namespace Technopro.Othello.Tests.E2E.WPF;

/// <summary>
/// GUI テストの前提となる出力ディレクトリの構成を確認するテスト。
/// ウィンドウを起動しないため、環境変数 OTHELLO_UI_TESTS の有無にかかわらず実行される。
/// </summary>
public class OutputLayoutTests
{
	/// <summary>
	/// テストの出力ディレクトリに起動対象の WPF 版 exe がコピーされていることを確認する。
	/// パス条件: AppContext.BaseDirectory 直下に Othello.WPF.exe が存在すること。
	/// </summary>
	[Fact]
	public void OutputDirectory_ContainsWpfExecutable()
	{
		var exePath = Path.Combine(AppContext.BaseDirectory, "Othello.WPF.exe");

		Assert.True(File.Exists(exePath), $"起動対象の exe が見つかりません: {exePath}");
	}

	/// <summary>
	/// テストの出力ディレクトリに Python AI のエントリポイントがコピーされていることを確認する。
	/// パス条件: AppContext.BaseDirectory\Othello.Python\ai.py が存在すること。
	/// </summary>
	[Fact]
	public void OutputDirectory_ContainsPythonAiScript()
	{
		var scriptPath = Path.Combine(AppContext.BaseDirectory, "Othello.Python", "ai.py");

		Assert.True(File.Exists(scriptPath), $"Python AI スクリプトが見つかりません: {scriptPath}");
	}
}
