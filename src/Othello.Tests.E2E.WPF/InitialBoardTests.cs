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

		Assert.Equal("2", session.GetElement("BlackScoreText").Name);
		Assert.Equal("2", session.GetElement("WhiteScoreText").Name);

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

		var validMoves = Enumerable.Range(1, 8)
			.SelectMany(row => Columns.Select(col => $"{col}{row}"))
			.Where(notation => session.GetElement(notation).Name.Contains("打てます"))
			.ToList();

		Assert.Equal(4, validMoves.Count);
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

		var result = Retry.WhileFalse(
			() => IsKnownEngine(badge.Name),
			TimeSpan.FromSeconds(10));

		Assert.True(result.Result, $"AI バッジが実エンジン名になりませんでした: \"{badge.Name}\"");
	}

	private static bool IsKnownEngine(string label)
		=> label.Contains("AI: Rust") || label.Contains("AI: Python") || label.Contains("AI: C#");
}
