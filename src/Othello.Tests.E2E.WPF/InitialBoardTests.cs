namespace Technopro.Othello.Tests.E2E.WPF;

using FlaUI.Core.Tools;
using Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>起動直後の盤面表示に関する GUI テスト（XAML バインディングの検証）。</summary>
public class InitialBoardTests
{
	/// <summary>盤面の列表記（左から a〜h）。</summary>
	private const string Columns = "abcdefgh";

	/// <summary>
	/// 起動直後の盤面が初期配置であることを確認する。
	/// パス条件: 黒・白のスコアがともに 2 で、石があるマスが中央の d4・e4・d5・e5 の 4 つだけであること。
	/// </summary>
	[UiFact]
	public void InitialBoard_HasTwoStonesEachAndOnlyCenterSquaresOccupied()
	{
		using var session = AppSession.Launch();

		// 盤面とスコアは AI 生成（Python 起動）完了後に反映されるため、初期値 2 になるまで待つ（レビュー指摘 PR #253）
		var blackScore = session.GetElement("BlackScoreText");
		var whiteScore = session.GetElement("WhiteScoreText");
		WaitUntil(
			() => blackScore.Name == "2" && whiteScore.Name == "2",
			() => $"初期スコアが 2 対 2 になりませんでした: 黒=\"{blackScore.Name}\", 白=\"{whiteScore.Name}\"");

		var occupied = Enumerable.Range(1, 8)
			.SelectMany(row => Columns.Select(col => $"{col}{row}"))
			.Where(notation => session.GetElement(notation).Name is { } name && (name.EndsWith("黒") || name.EndsWith("白")))
			.ToList();
		Assert.Equal(new[] { "d4", "e4", "d5", "e5" }.Order(), occupied.Order());
	}

	/// <summary>
	/// 人間が黒（先手）の初期盤面で、有効手が 4 マス表示されることを確認する。
	/// パス条件: Name に「打てます」を含むマスがちょうど 4 つあること。
	/// </summary>
	[UiFact]
	public void InitialBoard_ShowsFourValidMoves()
	{
		using var session = AppSession.Launch();

		// 有効手のハイライトも AI 生成完了後に反映されるため、4 マスになるまで待つ（レビュー指摘 PR #253）
		List<string> validMoves = [];
		WaitUntil(
			() =>
			{
				validMoves = Enumerable.Range(1, 8)
					.SelectMany(row => Columns.Select(col => $"{col}{row}"))
					.Where(notation => session.GetElement(notation).Name.Contains("打てます"))
					.ToList();
				return validMoves.Count == 4;
			},
			() => $"有効手が 4 マスになりませんでした: {validMoves.Count} マス ({string.Join(",", validMoves)})");
	}

	/// <summary>
	/// AI エンジン表示が、実際に使われるエンジン名になることを確認する。
	/// 起動直後は "AI: ?" の場合があるため、確定するまで再試行して待つ。
	/// パス条件: 表示が "AI: Rust" / "AI: Python" / "AI: C#" のいずれかを含むこと。
	/// </summary>
	[UiFact]
	public void AiBadge_ShowsActualEngineName()
	{
		using var session = AppSession.Launch();
		var badge = session.GetElement("AiEngineBadge");

		WaitUntil(
			() => IsKnownEngine(badge.Name),
			() => $"AI バッジが実エンジン名になりませんでした: \"{badge.Name}\"");
	}

	/// <summary>条件が満たされるまで最大 10 秒ポーリングし、満たされなければテストを失敗させる。</summary>
	/// <param name="condition">待機する条件。</param>
	/// <param name="failureMessage">タイムアウト時に評価する失敗メッセージ（最終状態を含められるよう遅延評価）。</param>
	private static void WaitUntil(Func<bool> condition, Func<string> failureMessage)
	{
		var result = Retry.WhileFalse(condition, TimeSpan.FromSeconds(10));
		Assert.True(result.Result, failureMessage());
	}

	private static bool IsKnownEngine(string label)
		=> label.Contains("AI: Rust") || label.Contains("AI: Python") || label.Contains("AI: C#");
}
