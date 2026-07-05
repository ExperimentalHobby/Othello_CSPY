namespace Technopro.Othello.Tests.WPF.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Technopro.Othello.Core.Models;
using Technopro.Othello.WPF.Converters;

/// <summary>PlayerColorToBrushConverter (WPF) の単体テスト。</summary>
public class PlayerColorToBrushConverterTests
{
    /// <summary>
    /// PlayerColor.Black を変換すると黒のブラシが返ることを確認する。
    /// パス条件: 戻り値が SolidColorBrush で Color が Colors.Black であること。
    /// </summary>
    [Fact]
    public void Convert_Black_ReturnsBlackBrush()
    {
        var converter = new PlayerColorToBrushConverter();
        var result = converter.Convert(PlayerColor.Black, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Black, brush.Color);
    }

    /// <summary>
    /// PlayerColor.White を変換すると白のブラシが返ることを確認する。
    /// パス条件: 戻り値が SolidColorBrush で Color が Colors.White であること。
    /// </summary>
    [Fact]
    public void Convert_White_ReturnsWhiteBrush()
    {
        var converter = new PlayerColorToBrushConverter();
        var result = converter.Convert(PlayerColor.White, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.White, brush.Color);
    }

    /// <summary>
    /// PlayerColor.Empty を変換すると透明ブラシが返ることを確認する。
    /// パス条件: 戻り値が SolidColorBrush で Color が Colors.Transparent であること。
    /// </summary>
    [Fact]
    public void Convert_Empty_ReturnsTransparentBrush()
    {
        var converter = new PlayerColorToBrushConverter();
        var result = converter.Convert(PlayerColor.Empty, typeof(Brush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Transparent, brush.Color);
    }

    /// <summary>
    /// ConvertBack を呼ぶと NotSupportedException がスローされることを確認する。
    /// パス条件: NotSupportedException がスローされること。
    /// </summary>
    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new PlayerColorToBrushConverter();
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(null, typeof(PlayerColor), null, CultureInfo.InvariantCulture));
    }
}

/// <summary>BoolToVisibilityConverter (WPF) の単体テスト。</summary>
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
        var result = converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);

        Assert.Equal(Visibility.Visible, result);
    }

    /// <summary>
    /// false を変換すると Visibility.Collapsed が返ることを確認する。
    /// パス条件: 戻り値が Visibility.Collapsed であること。
    /// </summary>
    [Fact]
    public void Convert_False_ReturnsCollapsed()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture);

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
        var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture);

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
        var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }
}

/// <summary>InverseBooleanConverter (WPF) の単体テスト。</summary>
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
        var result = converter.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture);

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
        var result = converter.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }
}
