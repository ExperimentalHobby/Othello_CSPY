namespace Technopro.Othello.ViewModels;

/// <summary>
/// スコア推移グラフ 1 点の座標（フレームワーク非依存）。
/// </summary>
public readonly record struct GraphPoint(double X, double Y);

/// <summary>
/// スコア推移グラフの座標計算ロジック（Issue #128: WPF/WinUI3 の RedrawScoreGraph 重複解消）。
/// WPF・WinUI3 いずれの `Point`/`PointCollection` 型にも依存しない純粋な計算のみを行い、
/// 各 UI プロジェクトは戻り値を自分の描画用の型に変換するだけにする。
/// </summary>
public static class ScoreGraphCalculator
{
	/// <summary>盤面のマス数（8×8）。石数を高さにスケーリングする際の基準値。</summary>
	private const int TotalSquares = 64;

	/// <summary>中央ガイド線の基準となる石数（引き分けの目安）。</summary>
	private const int MidLineCount = 32;

	/// <summary>
	/// 計算結果。黒・白それぞれの座標列、中央ガイド線の Y 座標、現在手を示す縦線の X 座標を保持する。
	/// </summary>
	public readonly record struct Result(
		IReadOnlyList<GraphPoint> BlackPoints,
		IReadOnlyList<GraphPoint> WhitePoints,
		double MidLineY,
		double CurrentMoveX);

	/// <summary>
	/// スコア履歴・描画領域の幅高さから、グラフ描画に必要な座標一式を計算する。
	/// </summary>
	/// <param name="history">スコア推移履歴（手数順）</param>
	/// <param name="width">描画領域の幅</param>
	/// <param name="height">描画領域の高さ</param>
	/// <returns>
	/// 描画に必要な座標一式。width・height が 0 以下、または history が空の場合は
	/// 描画できないため null を返す（呼び出し元は早期 return する）。
	/// </returns>
	public static Result? Calculate(IReadOnlyList<ScorePoint> history, double width, double height)
	{
		if (width <= 0 || height <= 0 || history.Count == 0)
			return null;

		double xScale = history.Count > 1 ? width / (history.Count - 1) : width;
		double yScale = height / TotalSquares;
		double midY = height - MidLineCount * yScale;

		var blackPoints = new List<GraphPoint>(history.Count);
		var whitePoints = new List<GraphPoint>(history.Count);
		for (int i = 0; i < history.Count; i++)
		{
			double x = history.Count > 1 ? i * xScale : 0;
			blackPoints.Add(new GraphPoint(x, height - history[i].BlackCount * yScale));
			whitePoints.Add(new GraphPoint(x, height - history[i].WhiteCount * yScale));
		}

		double currentMoveX = history.Count > 1 ? (history.Count - 1) * xScale : 0;

		return new Result(blackPoints, whitePoints, midY, currentMoveX);
	}
}
