namespace Technopro.Othello.Tests.WinUI3.Converters;

using Microsoft.UI.Xaml;
using Technopro.Othello.WinUI3.Converters;

// PlayerColorToBrushConverter は意図的にここではテストしない。
// SolidColorBrush の生成は WinUI3 の XAML ランタイム（Microsoft.UI.Xaml.Application.Start +
// DispatcherQueue）が動作していることを要求し、Bootstrap.TryInitialize だけでは
// COMException になることを実機検証で確認済み（Issue #157 調査）。
// フル XAML アプリケーションホストをテストプロセス内に立ち上げる複雑さは、本プロジェクトが
// 既に docs/gui-test-coverage-plan.md で明示的に回避を選択した「UI オートメーション」領域と
// 同種のコストであり、CI 安定性を優先して見送る。変換ロジック自体（どの色を選ぶか）は
// PlayerColorBrushRule として Othello.Tests の ConverterLogicTests.cs で既にカバー済み。

/// <summary>BoolToVisibilityConverter (WinUI3) の単体テスト。</summary>
public class BoolToVisibilityConverterTests
{
	/// <summary>
	/// true を変換すると Visibility.Visible が返ることを確認する。
	/// パス条件: 戻り値が Visibility.Visible であること。
	/// </summary>
	[Fact]
	public void Convert_True_ReturnsVisible()
	{
		var converter = new BoolToVisibilityConverter();
		var result = converter.Convert(true, typeof(Visibility), "", "");

		Assert.Equal(Visibility.Visible, result);
	}

	/// <summary>
	/// false を変換すると Visibility.Collapsed が返ることを確認する。
	/// WinUI3 は Visibility.Hidden を持たないため Collapsed になる（WPF との差異）。
	/// パス条件: 戻り値が Visibility.Collapsed であること。
	/// </summary>
	[Fact]
	public void Convert_False_ReturnsCollapsed()
	{
		var converter = new BoolToVisibilityConverter();
		var result = converter.Convert(false, typeof(Visibility), "", "");

		Assert.Equal(Visibility.Collapsed, result);
	}

	/// <summary>
	/// Visibility.Visible を逆変換すると true が返ることを確認する。
	/// パス条件: 戻り値が true であること。
	/// </summary>
	[Fact]
	public void ConvertBack_Visible_ReturnsTrue()
	{
		var converter = new BoolToVisibilityConverter();
		var result = converter.ConvertBack(Visibility.Visible, typeof(bool), "", "");

		Assert.Equal(true, result);
	}

	/// <summary>
	/// Visibility.Collapsed を逆変換すると false が返ることを確認する。
	/// パス条件: 戻り値が false であること。
	/// </summary>
	[Fact]
	public void ConvertBack_Collapsed_ReturnsFalse()
	{
		var converter = new BoolToVisibilityConverter();
		var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), "", "");

		Assert.Equal(false, result);
	}
}

/// <summary>InverseBooleanConverter (WinUI3) の単体テスト。</summary>
public class InverseBooleanConverterTests
{
	/// <summary>
	/// true を変換すると false が返ることを確認する。
	/// パス条件: 戻り値が false であること。
	/// </summary>
	[Fact]
	public void Convert_True_ReturnsFalse()
	{
		var converter = new InverseBooleanConverter();
		var result = converter.Convert(true, typeof(bool), "", "");

		Assert.Equal(false, result);
	}

	/// <summary>
	/// false を変換すると true が返ることを確認する。
	/// パス条件: 戻り値が true であること。
	/// </summary>
	[Fact]
	public void Convert_False_ReturnsTrue()
	{
		var converter = new InverseBooleanConverter();
		var result = converter.Convert(false, typeof(bool), "", "");

		Assert.Equal(true, result);
	}
}
