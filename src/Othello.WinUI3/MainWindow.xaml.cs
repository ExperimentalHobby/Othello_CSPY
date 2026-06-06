using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WinUI3;

public sealed partial class MainWindow : Window
{
    private readonly GameViewModel _viewModel;

    public MainWindow()
    {
        this.InitializeComponent();

        _viewModel = new GameViewModel();
        // WinUI3 は Window に DataContext がないため、XAML ルート要素に設定する
        if (this.Content is FrameworkElement root)
            root.DataContext = _viewModel;

        // ウィンドウサイズ・位置を設定
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1350, 1000));
        CenterWindow();

        this.Closed += (_, _) => _viewModel.Dispose();
    }

    private void CenterWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        AppWindow.Move(new Windows.Graphics.PointInt32(
            (displayArea.WorkArea.Width  - 1350) / 2,
            (displayArea.WorkArea.Height - 1000) / 2));
    }

    /// <summary>
    /// ボードのマスボタンがクリックされたときの処理。
    /// WinUI3 では RelativeSource AncestorType が使えないため、
    /// コードビハインドで DataContext から BoardSquareViewModel を取得して ViewModel に委譲する。
    /// </summary>
    private void OnSquareClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is BoardSquareViewModel sq)
            _viewModel.SquareClickedCommand.Execute(sq.Position);
    }

    // Border のサイズ変化に応じてセルサイズを動的更新し WPF の UniformGrid と同様に盤面を埋める
    private void OnBoardBorderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (BoardRepeater.Layout is not UniformGridLayout layout) return;

        // BorderThickness="4" なので各辺 4px ずつコンテンツ領域が縮む
        const double border = 4.0;
        double cellW = Math.Floor((e.NewSize.Width  - border * 2) / 8);
        double cellH = Math.Floor((e.NewSize.Height - border * 2) / 8);

        if (cellW >= 20 && cellH >= 20)
        {
            layout.MinItemWidth  = cellW;
            layout.MinItemHeight = cellH;
        }
    }
}
