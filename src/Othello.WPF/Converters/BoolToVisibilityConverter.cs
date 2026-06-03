using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Technopro.Othello.WPF.Converters;

/// <summary>
/// bool 値を Visibility に変換する XAML バインディング用コンバーター。
/// true → Visible、false → Hidden に変換する。
/// IsValidMove（有効手ハイライト）や HasPiece（石の表示）のバインディングで使用する。
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
    /// <returns>true → Visible、false または非 bool → Hidden</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Visible : Visibility.Hidden;

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
/// bool 値を反転した Visibility に変換する XAML バインディング用コンバーター。
/// true → Hidden、false → Visible に変換する。
/// IsAIThinking（AI 思考中はボードを非ヒットテスト化）のバインディングで使用する。
/// </summary>
public class BoolToVisibilityConverterInverted : IValueConverter
{
    /// <summary>
    /// bool から逆方向の Visibility に変換する。
    /// </summary>
    /// <param name="value">変換元の bool 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>true → Hidden、false または非 bool → Visible</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Hidden : Visibility.Visible;

    /// <summary>
    /// Visibility から bool に逆変換する。
    /// </summary>
    /// <param name="value">変換元の Visibility 値</param>
    /// <param name="targetType">使用しない</param>
    /// <param name="parameter">使用しない</param>
    /// <param name="culture">使用しない</param>
    /// <returns>Visible でない → true、Visible → false</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v != Visibility.Visible;
}
