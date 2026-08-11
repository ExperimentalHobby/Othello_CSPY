namespace Technopro.Othello.ViewModels.Converters;

using Technopro.Othello.Core.Models;

/// <summary>
/// bool → 表示可否 の変換ルール本体（Issue #128: WPF/WinUI3 の BoolToVisibilityConverter 重複解消）。
/// フレームワーク固有の型（WPF/WinUI3 の Visibility）には依存しない。
/// 各プラットフォームの IValueConverter は <see cref="IsVisible"/> の戻り値を
/// 自分の Visibility 列挙値にマッピングするだけにする。
/// </summary>
public static class BoolVisibilityRule
{
	/// <summary>true → 表示、false または非 bool → 非表示。</summary>
	public static bool IsVisible(object? value) => value is bool b && b;
}

/// <summary>
/// bool の反転ルール本体（Issue #128: WPF/WinUI3 の InverseBooleanConverter 重複解消）。
/// </summary>
public static class InverseBooleanRule
{
	/// <summary>true → false、false または非 bool → true。</summary>
	public static bool Invert(object? value) => !(value is bool b && b);
}

/// <summary>
/// PlayerColor → ブラシ種別 の変換ルール本体（Issue #128: WPF/WinUI3 の PlayerColorToBrushConverter 重複解消）。
/// 実際の SolidColorBrush インスタンス生成はフレームワーク固有のため各プラットフォーム側で行い、
/// この共通ロジックは「どの色を使うか」の判定のみを担う。
/// </summary>
public static class PlayerColorBrushRule
{
	/// <summary>変換先のブラシ種別。</summary>
	public enum BrushKind { Black, White, Transparent }

	/// <summary>PlayerColor.Black → Black、White → White、それ以外（Empty・非 PlayerColor 含む）→ Transparent。</summary>
	public static BrushKind Resolve(object? value) =>
		value is PlayerColor pc
			? pc switch
			{
				PlayerColor.Black => BrushKind.Black,
				PlayerColor.White => BrushKind.White,
				_                 => BrushKind.Transparent
			}
			: BrushKind.Transparent;
}
