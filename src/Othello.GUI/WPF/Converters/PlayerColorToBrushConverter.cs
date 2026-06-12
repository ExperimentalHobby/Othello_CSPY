using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.WPF.Converters;

/// <summary>
/// PlayerColor を WPF の SolidColorBrush に変換する。
/// BoardSquareViewModel から SolidColorBrush を削除した代替として使用する。
/// </summary>
public class PlayerColorToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush BlackBrush       = Frozen(Colors.Black);
    private static readonly SolidColorBrush WhiteBrush       = Frozen(Colors.White);
    private static readonly SolidColorBrush TransparentBrush = Frozen(Colors.Transparent);

    private static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is PlayerColor pc
            ? pc switch
            {
                PlayerColor.Black => BlackBrush,
                PlayerColor.White => WhiteBrush,
                _                 => TransparentBrush
            }
            : TransparentBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
