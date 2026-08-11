using Microsoft.UI.Xaml.Data;
using Technopro.Othello.ViewModels.Converters;

namespace Technopro.Othello.WinUI3.Converters;

/// <summary>
/// bool 値を反転して bool を返す WinUI3 用コンバーター。
/// IsHitTestVisible のような bool プロパティに「AI 思考中は false」を渡す用途で使用する。
/// 変換ルール本体は <see cref="InverseBooleanRule"/>（WPF/WinUI3 共通）に委譲する（Issue #128）。
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        InverseBooleanRule.Invert(value);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        InverseBooleanRule.Invert(value);
}
