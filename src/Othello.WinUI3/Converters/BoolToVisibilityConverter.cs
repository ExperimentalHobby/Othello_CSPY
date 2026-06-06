using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Technopro.Othello.WinUI3.Converters;

/// <summary>
/// bool を Visibility に変換する WinUI3 用コンバーター。
/// WinUI3 は Visibility.Hidden を持たないため Collapsed を使用する。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v == Visibility.Visible;
}
