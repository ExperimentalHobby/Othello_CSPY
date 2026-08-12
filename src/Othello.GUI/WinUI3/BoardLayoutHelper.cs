using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Technopro.Othello.WinUI3;

/// <summary>
/// 盤面（8×8）を表示する UniformGridLayout のセルサイズを、コンテナのサイズ変更に応じて
/// 更新する共通ロジック。MainWindow / KifuWindow の両方から呼び出される（Issue #128）。
/// </summary>
internal static class BoardLayoutHelper
{
	/// <summary>Border の枠線太さ（BorderThickness="4"）。片側 4px ずつコンテンツ領域が縮む。</summary>
	private const double BorderThickness = 4.0;

	/// <summary>これ未満のセルサイズにはしない（極端に小さいウィンドウでのレイアウト崩れ防止）。</summary>
	private const double MinCellSize = 20.0;

	/// <summary>
	/// newSize（枠線を含むコンテナのサイズ）から 8×8 分のセルサイズを計算し、layout に反映する。
	/// Math.Floor を使わず正確な値を渡す: ItemsStretch="Fill" が横方向を、
	/// MinItemHeight の正確な値が縦方向の隙間をなくす。
	/// </summary>
	/// <param name="layout">更新対象の UniformGridLayout</param>
	/// <param name="newSize">SizeChanged イベントの NewSize（枠線を含むコンテナのサイズ）</param>
	public static void UpdateCellSize(UniformGridLayout layout, Size newSize)
	{
		double cellW = (newSize.Width - BorderThickness * 2) / 8;
		double cellH = (newSize.Height - BorderThickness * 2) / 8;

		if (cellW >= MinCellSize && cellH >= MinCellSize)
		{
			layout.MinItemWidth = cellW;
			layout.MinItemHeight = cellH;
		}
	}
}
