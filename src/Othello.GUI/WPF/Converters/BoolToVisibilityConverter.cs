using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Technopro.Othello.WPF.Converters;

/// <summary>
/// bool 値を Visibility に変換する XAML バインディング用コンバーター。
/// true → Visible、false → Collapsed に変換する。
/// Collapsed はレイアウト上のスペースも解放するため、
/// パネルの表示切替（IsCpuVsCpu / IsHumanVsCpu）に使うとスペースが詰まる。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// bool から Visibility に変換する。
    /// </summary>
    /// <param name="value">変換元の bool 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>true → Visible、false または非 bool → Collapsed</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Visibility から bool に逆変換する（TwoWay バインディング用）。
    /// </summary>
    /// <param name="value">変換元の Visibility 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>Visible → true、それ以外 → false</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

/// <summary>
/// bool 値を反転して bool を返す XAML バインディング用コンバーター。
/// IsHitTestVisible のような bool プロパティに「AI 思考中は false」を渡す用途で使用する。
/// （Visibility を返すコンバーターを bool プロパティに誤バインドしないようにするための専用変換器。）
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// bool を反転する。
    /// </summary>
    /// <param name="value">変換元の bool 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>true → false、false または非 bool → true</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is bool b && b);

    /// <summary>
    /// bool を反転して返す（双方向対応）。
    /// </summary>
    /// <param name="value">変換元の bool 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>true → false、false または非 bool → true</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is bool b && b);
}
