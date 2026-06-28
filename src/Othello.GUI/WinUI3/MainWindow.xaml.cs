using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.ViewModels;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace Technopro.Othello.WinUI3;

public sealed partial class MainWindow : Window
{
    /// <summary>初期ウィンドウ幅（px）</summary>
    private const int WindowWidth = 1350;

    /// <summary>初期ウィンドウ高さ（px）</summary>
    private const int WindowHeight = 1000;

    private readonly GameViewModel _viewModel;

    public MainWindow()
    {
        this.InitializeComponent();

        _viewModel = new GameViewModel();
        // WinUI3 は Window に DataContext がないため、XAML ルート要素に設定する
        if (this.Content is FrameworkElement root)
        {
            root.DataContext = _viewModel;
            // Loaded 後に非同期でゲーム開始することでウィンドウ表示をブロックしない
            root.Loaded += async (_, _) =>
            {
                await _viewModel.StartNewGameAsync();
                _viewModel.ScoreHistory.CollectionChanged += OnScoreHistoryChanged;
                RedrawScoreGraph();
            };
        }

        // ウィンドウサイズ・位置を設定
        AppWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
        CenterWindow();

        this.Closed += (_, _) => _viewModel.Dispose();

        // 各マスの IsBeingFlipped 変更を購読して反転アニメーションをトリガーする
        foreach (var sq in _viewModel.BoardSquares)
            sq.PropertyChanged += OnSquarePropertyChanged;
    }

    private void CenterWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        AppWindow.Move(new Windows.Graphics.PointInt32(
            (displayArea.WorkArea.Width  - WindowWidth)  / 2,
            (displayArea.WorkArea.Height - WindowHeight) / 2));
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
    /// <summary>
    /// BoardSquareViewModel の PropertyChanged を受け、IsBeingFlipped が true になったマスの
    /// Composition Scale アニメーションをトリガーする（Y 軸 1→0→1、300ms）。
    /// </summary>
    private void OnSquarePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BoardSquareViewModel.IsBeingFlipped)) return;
        if (sender is not BoardSquareViewModel sq || !sq.IsBeingFlipped) return;

        int index = _viewModel.BoardSquares.IndexOf(sq);
        if (index < 0) return;

        DispatcherQueue.TryEnqueue(() => AnimateFlip(index));
    }

    /// <summary>
    /// 指定インデックスのセル内にある石 Ellipse のみに Y 軸スケール反転アニメーション（300ms）を適用する。
    /// DataTemplate 内の Ellipse は [0]=有効手マーカー（黄）・[1]=石 の順なので Skip(1) で石を取得する。
    /// セル全体（Button）ではなく石だけを対象にすることで WPF 版と同じ挙動になる。
    /// </summary>
    private void AnimateFlip(int index)
    {
        if (BoardRepeater.TryGetElement(index) is not FrameworkElement element) return;

        // VisualTreeHelper でセル内の Ellipse を列挙し、2 番目（石）だけを取得する
        var stoneEllipse = FindDescendants<Ellipse>(element).Skip(1).FirstOrDefault();
        if (stoneEllipse == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(stoneEllipse);
        var compositor = visual.Compositor;

        var animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(0.0f, new Vector3(1f, 1f, 1f));
        animation.InsertKeyFrame(0.5f, new Vector3(1f, 0f, 1f)); // Y 軸を潰す（石の色が切り替わるタイミング）
        animation.InsertKeyFrame(1.0f, new Vector3(1f, 1f, 1f));
        animation.Duration = TimeSpan.FromMilliseconds(300);

        visual.CenterPoint = new Vector3(
            (float)(stoneEllipse.ActualWidth  / 2),
            (float)(stoneEllipse.ActualHeight / 2),
            0f);
        visual.StartAnimation("Scale", animation);
    }

    /// <summary>
    /// ビジュアルツリーを深さ優先で走査して型 T の子孫を列挙する。
    /// </summary>
    private static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var d in FindDescendants<T>(child))
                yield return d;
        }
    }

    private void OnScoreHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RedrawScoreGraph();

    private void OnScoreCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        => RedrawScoreGraph();

    private void RedrawScoreGraph()
    {
        var history = _viewModel.ScoreHistory;
        var w = ScoreCanvas.ActualWidth;
        var h = ScoreCanvas.ActualHeight;
        if (w <= 0 || h <= 0 || history.Count == 0) return;

        double xScale = history.Count > 1 ? w / (history.Count - 1) : w;
        double yScale = h / 64.0;

        // 中央ガイド線（石数 32）
        double midY = h - 32 * yScale;
        MidLine.X1 = 0; MidLine.X2 = w;
        MidLine.Y1 = midY; MidLine.Y2 = midY;

        var blackPoints = new PointCollection();
        var whitePoints = new PointCollection();
        for (int i = 0; i < history.Count; i++)
        {
            double x = history.Count > 1 ? i * xScale : 0;
            blackPoints.Add(new Point(x, h - history[i].BlackCount * yScale));
            whitePoints.Add(new Point(x, h - history[i].WhiteCount * yScale));
        }
        BlackScoreLine.Points = blackPoints;
        WhiteScoreLine.Points = whitePoints;

        double cx = history.Count > 1 ? (history.Count - 1) * xScale : 0;
        CurrentMoveLine.X1 = cx; CurrentMoveLine.X2 = cx;
        CurrentMoveLine.Y1 = 0;  CurrentMoveLine.Y2 = h;
    }

    private void OnTimeLimitSecondsLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            ApplyTimeLimitFromTextBox(tb);
        _viewModel.SaveTimeLimitSettings();
    }

    private void OnTimeLimitSecondsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox tb)
        {
            ApplyTimeLimitFromTextBox(tb);
            _viewModel.SaveTimeLimitSettings();
            e.Handled = true;
        }
    }

    private void ApplyTimeLimitFromTextBox(TextBox tb)
    {
        if (int.TryParse(tb.Text, out int seconds) && seconds >= 1)
            _viewModel.TimeLimitSeconds = seconds;
        else
            tb.Text = _viewModel.TimeLimitSeconds.ToString();
    }

    private async void OnSaveKifu(object sender, RoutedEventArgs e)
    {
        var record = _viewModel.LastKifuRecord;
        if (record is null)
        {
            var noKifuDlg = new ContentDialog
            {
                Title   = "棋譜を保存",
                Content = "保存できる棋譜がありません。ゲームを終了させてください。",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await noKifuDlg.ShowAsync();
            return;
        }

        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("棋譜ファイル", new[] { ".json" });
        picker.SuggestedFileName = $"kifu_{record.PlayedAt.LocalDateTime:yyyyMMdd_HHmmss}";

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await KifuSerializer.SaveAsync(record, file.Path);
        }
    }

    private async void OnOpenKifu(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var record = await KifuSerializer.LoadAsync(file.Path);
        if (record is null)
        {
            var errDlg = new ContentDialog
            {
                Title   = "棋譜を開く",
                Content = "棋譜ファイルを読み込めませんでした。",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await errDlg.ShowAsync();
            return;
        }

        var player = new KifuPlayer(record);
        var vm     = new KifuViewModel(player, record);
        var win    = new KifuWindow(vm);
        win.Activate();
    }

    private void OnBoardBorderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (BoardRepeater.Layout is not UniformGridLayout layout) return;

        // BorderThickness="4" なので各辺 4px ずつコンテンツ領域が縮む
        // Math.Floor を使わず正確な値を渡す: ItemsStretch="Fill" が横方向を、
        // MinItemHeight の正確な値が縦方向の隙間をなくす
        const double border = 4.0;
        double cellW = (e.NewSize.Width  - border * 2) / 8;
        double cellH = (e.NewSize.Height - border * 2) / 8;

        if (cellW >= 20 && cellH >= 20)
        {
            layout.MinItemWidth  = cellW;
            layout.MinItemHeight = cellH;
        }
    }
}
