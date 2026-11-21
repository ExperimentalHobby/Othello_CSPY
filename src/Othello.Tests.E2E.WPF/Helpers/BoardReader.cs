namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>起動中アプリの盤面を UI Automation 経由で読み取るヘルパー。</summary>
public static class BoardReader
{
	/// <summary>盤面の列表記（左から a〜h）。</summary>
	private const string Columns = "abcdefgh";

	/// <summary>全 64 マスの表記（"a1"〜"h8"）。</summary>
	public static IReadOnlyList<string> AllNotations { get; } = Enumerable.Range(1, 8)
		.SelectMany(row => Columns.Select(col => $"{col}{row}"))
		.ToList();

	/// <summary>石が置かれているマスの表記を返す（Name が「黒」「白」で終わるマス）。</summary>
	public static List<string> OccupiedSquares(AppSession session)
		=> AllNotations
			.Where(notation => session.GetElement(notation).Name is { } name && (name.EndsWith("黒") || name.EndsWith("白")))
			.ToList();

	/// <summary>有効手として表示されているマスの表記を返す（Name に「打てます」を含むマス）。</summary>
	public static List<string> ValidMoves(AppSession session)
		=> AllNotations
			.Where(notation => session.GetElement(notation).Name.Contains("打てます"))
			.ToList();
}
