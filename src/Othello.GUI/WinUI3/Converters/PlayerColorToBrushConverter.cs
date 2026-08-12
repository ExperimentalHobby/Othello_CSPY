using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Technopro.Othello.ViewModels.Converters;

namespace Technopro.Othello.WinUI3.Converters;

/// <summary>
/// PlayerColor を WinUI3 の SolidColorBrush に変換する。
/// どの色を使うかの判定は <see cref="PlayerColorBrushRule"/>（WPF/WinUI3 共通）に委譲し、
/// このクラスは WinUI3 固有の SolidColorBrush インスタンスへのマッピングのみを担う（Issue #128）。
/// </summary>
public class PlayerColorToBrushConverter : IValueConverter
{
	private static readonly SolidColorBrush BlackBrush = new(Colors.Black);
	private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
	private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

	public object Convert(object value, Type targetType, object parameter, string language) =>
		PlayerColorBrushRule.Resolve(value) switch
		{
			PlayerColorBrushRule.BrushKind.Black => BlackBrush,
			PlayerColorBrushRule.BrushKind.White => WhiteBrush,
			_ => TransparentBrush
		};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
