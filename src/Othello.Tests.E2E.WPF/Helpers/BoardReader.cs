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

	/// <summary>初期配置で石がある中央 4 マスと、その Name（d4・e5 が白、e4・d5 が黒）。</summary>
	private static readonly IReadOnlyDictionary<string, string> InitialStones = new Dictionary<string, string>
	{
		["d4"] = "d4 白",
		["e4"] = "e4 黒",
		["d5"] = "d5 黒",
		["e5"] = "e5 白",
	};

	/// <summary>人間が黒（先手）のときの初期配置での有効手 4 マス。</summary>
	private static readonly IReadOnlySet<string> InitialValidMoves = new HashSet<string> { "d3", "c4", "f5", "e6" };

	/// <summary>
	/// 現在の盤面が「人間が黒の初期盤面」とどこが違うかを返す。空なら一致している。
	/// 中央 4 マスの色、それ以外のマスが空きであること、有効手がちょうど 4 マスであることを全 64 マスで確認する。
	/// </summary>
	public static List<string> InitialPositionMismatches(AppSession session)
	{
		var mismatches = new List<string>();
		foreach (var notation in AllNotations)
		{
			var expected = InitialStones.TryGetValue(notation, out var stone) ? stone
				: InitialValidMoves.Contains(notation) ? $"{notation} 空き 打てます"
				: $"{notation} 空き";
			var actual = session.GetElement(notation).Name;
			if (actual != expected)
				mismatches.Add($"{notation}: 期待=\"{expected}\", 実際=\"{actual}\"");
		}
		return mismatches;
	}
}
