namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Tests.Helpers;

/// <summary>
/// PythonSubprocessAI の異常系（クラッシュ・タイムアウト・不正応答）の結合テスト（Issue #159）。
///
/// 正常系の結合テスト（PythonSubprocessAIIntegrationTests）は実際の ai.py を起動して
/// 合法手が返ることのみを検証しており、Python プロセスが異常終了した場合・応答がタイムアウト
/// した場合・不正な JSON を返した場合に PythonSubprocessAI がクラッシュせず適切な例外を
/// 送出できるかを検証するテストが無かった。
///
/// ai.py の代わりに、意図的に異常な挙動をする最小の Python スクリプトをテストごとに
/// 一時ファイルへ書き出して使用する。ai.py 自体の存在（File.Exists(ScriptPath)）を
/// 「テスト環境に動作する Python 3 セットアップがあるか」の目印として使う既存の結合テストと
/// 同じスキップ条件を踏襲する。
/// </summary>
public class PythonSubprocessAIFailureModeTests : IDisposable
{
	private static readonly string AiPyPath = AiScriptPaths.AiScriptPath;
	private readonly List<string> _tempScripts = new();

	/// <summary>指定した Python コードを一時ファイルへ書き出し、そのパスを返す。</summary>
	private string WriteFixtureScript(string pythonCode)
	{
		var path = Path.Combine(Path.GetTempPath(), $"othello_ai_fixture_{Guid.NewGuid():N}.py");
		File.WriteAllText(path, pythonCode);
		_tempScripts.Add(path);
		return path;
	}

	public void Dispose()
	{
		foreach (var path in _tempScripts)
		{
			try { File.Delete(path); } catch { /* ベストエフォート */ }
		}
	}

	/// <summary>
	/// Python プロセスが起動直後（ハンドシェイク前）に例外を送出して異常終了した場合、
	/// GetBestMove が stderr の内容を含む InvalidOperationException を送出することを確認する。
	/// パス条件: InvalidOperationException がスローされ、メッセージに Python 側の例外内容
	/// （RuntimeError）が含まれること。
	/// </summary>
	[Fact]
	public void GetBestMove_ProcessCrashesImmediately_ThrowsWithStderrDetails()
	{
		if (!File.Exists(AiPyPath)) return;

		var scriptPath = WriteFixtureScript(
			"raise RuntimeError('intentional test crash for PythonSubprocessAI failure-mode test')\n");

		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, scriptPath);

		var ex = Assert.Throws<InvalidOperationException>(
			() => ai.GetBestMove(TestBoardHelper.CreateInitialBoard(), PlayerColor.Black));

		Assert.Contains("RuntimeError", ex.Message);
	}

	/// <summary>
	/// Python プロセスがハンドシェイク後に応答を返さず無応答のまま固まった場合、
	/// GetBestMove が TimeoutException を送出し、プロセスを強制終了することを確認する。
	/// パス条件: TimeoutException がスローされ、その後 Python プロセスが終了していること。
	/// </summary>
	[Fact]
	public void GetBestMove_ProcessNeverResponds_ThrowsTimeoutAndKillsProcess()
	{
		if (!File.Exists(AiPyPath)) return;

		var scriptPath = WriteFixtureScript(
			"""
			import json, sys, time
			print(json.dumps({"backend": "python"}))
			sys.stdout.flush()
			for line in sys.stdin:
			    time.sleep(9999)
			""");

		// 応答待ちタイムアウトを 1 秒に短縮して検証する（既定の 60 秒を待たない）。
		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, scriptPath, responseTimeoutMsOverride: 1000);

		Assert.Throws<TimeoutException>(
			() => ai.GetBestMove(TestBoardHelper.CreateInitialBoard(), PlayerColor.Black));
	}

	/// <summary>
	/// Python プロセスが構文的に不正な JSON を返した場合、GetBestMove が
	/// JsonException を送出することを確認する（無応答のハングや不正な着手の
	/// 静かな採用にならないこと）。
	/// パス条件: System.Text.Json.JsonException がスローされること。
	/// </summary>
	[Fact]
	public void GetBestMove_MalformedJsonResponse_ThrowsJsonException()
	{
		if (!File.Exists(AiPyPath)) return;

		var scriptPath = WriteFixtureScript(
			"""
			import json, sys
			print(json.dumps({"backend": "python"}))
			sys.stdout.flush()
			for line in sys.stdin:
			    print("not valid json {{{")
			    sys.stdout.flush()
			""");

		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, scriptPath);

		Assert.ThrowsAny<System.Text.Json.JsonException>(
			() => ai.GetBestMove(TestBoardHelper.CreateInitialBoard(), PlayerColor.Black));
	}

	/// <summary>
	/// Python プロセスが構文的には正しいが row/col フィールドを含まない JSON を返した場合、
	/// GetBestMove が例外を送出することを確認する（未定義の着手を静かに採用しないこと）。
	/// パス条件: 例外がスローされること（KeyNotFoundException 相当）。
	/// </summary>
	[Fact]
	public void GetBestMove_ResponseMissingExpectedFields_Throws()
	{
		if (!File.Exists(AiPyPath)) return;

		var scriptPath = WriteFixtureScript(
			"""
			import json, sys
			print(json.dumps({"backend": "python"}))
			sys.stdout.flush()
			for line in sys.stdin:
			    print(json.dumps({"unexpected": "field"}))
			    sys.stdout.flush()
			""");

		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, scriptPath);

		Assert.ThrowsAny<Exception>(
			() => ai.GetBestMove(TestBoardHelper.CreateInitialBoard(), PlayerColor.Black));
	}

	/// <summary>
	/// Python プロセスが明示的に {"error": ...} を返した場合、GetBestMove がその内容を
	/// 含む InvalidOperationException を送出することを確認する。
	/// パス条件: InvalidOperationException がスローされ、メッセージに Python 側のエラー
	/// 文言が含まれること。
	/// </summary>
	[Fact]
	public void GetBestMove_ErrorFieldInResponse_ThrowsWithMessage()
	{
		if (!File.Exists(AiPyPath)) return;

		var scriptPath = WriteFixtureScript(
			"""
			import json, sys
			print(json.dumps({"backend": "python"}))
			sys.stdout.flush()
			for line in sys.stdin:
			    print(json.dumps({"error": "simulated python-side failure"}))
			    sys.stdout.flush()
			""");

		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, scriptPath);

		var ex = Assert.Throws<InvalidOperationException>(
			() => ai.GetBestMove(TestBoardHelper.CreateInitialBoard(), PlayerColor.Black));

		Assert.Contains("simulated python-side failure", ex.Message);
	}
}
