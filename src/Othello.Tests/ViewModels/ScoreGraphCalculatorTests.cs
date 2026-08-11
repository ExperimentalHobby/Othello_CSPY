namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.ViewModels;

/// <summary>
/// ScoreGraphCalculator の単体テスト（Issue #128: WPF/WinUI3 の RedrawScoreGraph 座標計算の共通化）。
/// </summary>
public class ScoreGraphCalculatorTests
{
	/// <summary>
	/// 幅・高さが 0 以下の場合は null を返すことを確認する。
	/// パス条件: width=0 または height=0 で戻り値が null であること。
	/// </summary>
	[Fact]
	public void Calculate_ZeroWidthOrHeight_ReturnsNull()
	{
		var history = new List<ScorePoint> { new(0, 2, 2) };

		Assert.Null(ScoreGraphCalculator.Calculate(history, width: 0, height: 100));
		Assert.Null(ScoreGraphCalculator.Calculate(history, width: 100, height: 0));
	}

	/// <summary>
	/// 履歴が空の場合は null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void Calculate_EmptyHistory_ReturnsNull()
	{
		var history = new List<ScorePoint>();

		Assert.Null(ScoreGraphCalculator.Calculate(history, width: 100, height: 100));
	}

	/// <summary>
	/// 履歴が 1 件のみの場合、x 座標は 0、y 座標は石数を高さにスケーリングした値になることを確認する。
	/// 盤面は 64 マスのため yScale = height / 64。
	/// パス条件: BlackPoints[0] == (0, height - blackCount*yScale)。
	/// </summary>
	[Fact]
	public void Calculate_SinglePoint_PlacesAtOriginX()
	{
		var history = new List<ScorePoint> { new(0, 2, 2) };

		var result = ScoreGraphCalculator.Calculate(history, width: 200, height: 640);

		Assert.NotNull(result);
		double yScale = 640 / 64.0;
		Assert.Single(result!.Value.BlackPoints);
		Assert.Equal(0, result.Value.BlackPoints[0].X);
		Assert.Equal(640 - 2 * yScale, result.Value.BlackPoints[0].Y);
		// 履歴 1 件のときの現在手 X 座標も 0
		Assert.Equal(0, result.Value.CurrentMoveX);
	}

	/// <summary>
	/// 複数点の履歴で、x 座標が幅方向に等間隔でスケーリングされることを確認する。
	/// パス条件: 3 点の履歴で xScale = width/(count-1) となり、2 点目の x が xScale と一致すること。
	/// </summary>
	[Fact]
	public void Calculate_MultiplePoints_ScalesXEvenly()
	{
		var history = new List<ScorePoint> { new(0, 2, 2), new(1, 3, 1), new(2, 4, 0) };

		var result = ScoreGraphCalculator.Calculate(history, width: 200, height: 640);

		Assert.NotNull(result);
		double xScale = 200 / (double)(history.Count - 1);
		Assert.Equal(0, result!.Value.BlackPoints[0].X);
		Assert.Equal(xScale, result.Value.BlackPoints[1].X);
		Assert.Equal(2 * xScale, result.Value.BlackPoints[2].X);
		// 現在手 X 座標は最終点の x と一致する
		Assert.Equal(2 * xScale, result.Value.CurrentMoveX);
	}

	/// <summary>
	/// 中央ガイド線の Y 座標が石数 32 の位置（height - 32*yScale）になることを確認する。
	/// パス条件: MidLineY == height - 32*(height/64.0)（= height/2）であること。
	/// </summary>
	[Fact]
	public void Calculate_MidLineY_IsHalfHeight()
	{
		var history = new List<ScorePoint> { new(0, 2, 2) };

		var result = ScoreGraphCalculator.Calculate(history, width: 200, height: 640);

		Assert.NotNull(result);
		Assert.Equal(320, result!.Value.MidLineY);
	}

	/// <summary>
	/// 黒・白それぞれの座標列の件数が履歴件数と一致することを確認する。
	/// パス条件: BlackPoints.Count == WhitePoints.Count == history.Count。
	/// </summary>
	[Fact]
	public void Calculate_PointCounts_MatchHistoryCount()
	{
		var history = new List<ScorePoint> { new(0, 2, 2), new(1, 3, 1), new(2, 4, 0), new(3, 5, 0) };

		var result = ScoreGraphCalculator.Calculate(history, width: 200, height: 640);

		Assert.NotNull(result);
		Assert.Equal(history.Count, result!.Value.BlackPoints.Count);
		Assert.Equal(history.Count, result.Value.WhitePoints.Count);
	}
}
