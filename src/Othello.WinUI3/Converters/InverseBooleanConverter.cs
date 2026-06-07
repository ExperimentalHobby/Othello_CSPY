using Microsoft.UI.Xaml.Data;

namespace Technopro.Othello.WinUI3.Converters;

/// <summary>
/// bool 値を反転して bool を返す WinUI3 用コンバーター。
/// IsHitTestVisible のような bool プロパティに「AI 思考中は false」を渡す用途で使用する。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        !(value is bool b && b);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        !(value is bool b && b);
}
