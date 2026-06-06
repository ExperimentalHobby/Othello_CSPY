using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.WinUI3.Converters;

/// <summary>
/// PlayerColor を WinUI3 の SolidColorBrush に変換する。
/// </summary>
public class PlayerColorToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush BlackBrush       = new(Colors.Black);
    private static readonly SolidColorBrush WhiteBrush       = new(Colors.White);
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is PlayerColor pc ? pc switch
        {
            PlayerColor.Black => BlackBrush,
            PlayerColor.White => WhiteBrush,
            _                 => TransparentBrush
        } : TransparentBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
