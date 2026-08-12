namespace Technopro.Othello.Tests.ViewModels.Converters;

using Technopro.Othello.Core.Models;
using Technopro.Othello.ViewModels.Converters;

/// <summary>
/// ConverterLogic の単体テスト（Issue #128: WPF/WinUI3 コンバーターの変換ルール本体の共通化）。
/// フレームワーク型（Visibility・SolidColorBrush 等）に依存しない純粋な変換ルールのみを検証する。
/// </summary>
public class BoolVisibilityRuleTests
{
	/// <summary>
	/// true を渡すと true（Visible 相当）を返すことを確認する。
	/// パス条件: 戻り値が true であること。
	/// </summary>
	[Fact]
	public void IsVisible_True_ReturnsTrue()
		=> Assert.True(BoolVisibilityRule.IsVisible(true));

	/// <summary>
	/// false を渡すと false（Collapsed 相当）を返すことを確認する。
	/// パス条件: 戻り値が false であること。
	/// </summary>
	[Fact]
	public void IsVisible_False_ReturnsFalse()
		=> Assert.False(BoolVisibilityRule.IsVisible(false));

	/// <summary>
	/// bool 以外の値（null 等）を渡すと false を返すことを確認する。
	/// パス条件: 戻り値が false であること。
	/// </summary>
	[Fact]
	public void IsVisible_NonBool_ReturnsFalse()
		=> Assert.False(BoolVisibilityRule.IsVisible(null));
}

public class InverseBooleanRuleTests
{
	/// <summary>
	/// true を渡すと false が返ることを確認する。
	/// パス条件: 戻り値が false であること。
	/// </summary>
	[Fact]
	public void Invert_True_ReturnsFalse()
		=> Assert.False(InverseBooleanRule.Invert(true));

	/// <summary>
	/// false を渡すと true が返ることを確認する。
	/// パス条件: 戻り値が true であること。
	/// </summary>
	[Fact]
	public void Invert_False_ReturnsTrue()
		=> Assert.True(InverseBooleanRule.Invert(false));

	/// <summary>
	/// bool 以外の値（null 等）を渡すと true が返ることを確認する。
	/// パス条件: 戻り値が true であること。
	/// </summary>
	[Fact]
	public void Invert_NonBool_ReturnsTrue()
		=> Assert.True(InverseBooleanRule.Invert(null));
}

public class PlayerColorBrushRuleTests
{
	/// <summary>
	/// PlayerColor.Black を渡すと BrushKind.Black が返ることを確認する。
	/// パス条件: 戻り値が BrushKind.Black であること。
	/// </summary>
	[Fact]
	public void Resolve_Black_ReturnsBlack()
		=> Assert.Equal(PlayerColorBrushRule.BrushKind.Black, PlayerColorBrushRule.Resolve(PlayerColor.Black));

	/// <summary>
	/// PlayerColor.White を渡すと BrushKind.White が返ることを確認する。
	/// パス条件: 戻り値が BrushKind.White であること。
	/// </summary>
	[Fact]
	public void Resolve_White_ReturnsWhite()
		=> Assert.Equal(PlayerColorBrushRule.BrushKind.White, PlayerColorBrushRule.Resolve(PlayerColor.White));

	/// <summary>
	/// PlayerColor.Empty を渡すと BrushKind.Transparent が返ることを確認する。
	/// パス条件: 戻り値が BrushKind.Transparent であること。
	/// </summary>
	[Fact]
	public void Resolve_Empty_ReturnsTransparent()
		=> Assert.Equal(PlayerColorBrushRule.BrushKind.Transparent, PlayerColorBrushRule.Resolve(PlayerColor.Empty));

	/// <summary>
	/// PlayerColor 以外の値（null 等）を渡すと BrushKind.Transparent が返ることを確認する。
	/// パス条件: 戻り値が BrushKind.Transparent であること。
	/// </summary>
	[Fact]
	public void Resolve_NonPlayerColor_ReturnsTransparent()
		=> Assert.Equal(PlayerColorBrushRule.BrushKind.Transparent, PlayerColorBrushRule.Resolve(null));
}
