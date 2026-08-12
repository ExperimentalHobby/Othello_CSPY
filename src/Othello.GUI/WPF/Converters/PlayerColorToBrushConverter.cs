using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Technopro.Othello.ViewModels.Converters;

namespace Technopro.Othello.WPF.Converters;

/// <summary>
/// PlayerColor を WPF の SolidColorBrush に変換する。
/// BoardSquareViewModel から SolidColorBrush を削除した代替として使用する。
/// どの色を使うかの判定は <see cref="PlayerColorBrushRule"/>（WPF/WinUI3 共通）に委譲し、
/// このクラスは WPF 固有の SolidColorBrush インスタンスへのマッピングのみを担う（Issue #128）。
/// </summary>
public class PlayerColorToBrushConverter : IValueConverter
{
	private static readonly SolidColorBrush BlackBrush = Frozen(Colors.Black);
	private static readonly SolidColorBrush WhiteBrush = Frozen(Colors.White);
	private static readonly SolidColorBrush TransparentBrush = Frozen(Colors.Transparent);

	private static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		PlayerColorBrushRule.Resolve(value) switch
		{
			PlayerColorBrushRule.BrushKind.Black => BlackBrush,
			PlayerColorBrushRule.BrushKind.White => WhiteBrush,
			_ => TransparentBrush
		};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
