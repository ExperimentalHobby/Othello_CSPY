using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WPF;

public partial class MainWindow : Window
{
    private GameViewModel _vm = null!;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new GameViewModel();
        DataContext = _vm;

        Loaded  += async (_, _) =>
        {
            await _vm.StartNewGameAsync();
            _vm.ScoreHistory.CollectionChanged += OnScoreHistoryChanged;
            RedrawScoreGraph();
        };
        Closed  += (_, _) => (_vm as IDisposable)?.Dispose();
    }

    private void OnScoreHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RedrawScoreGraph();

    private void OnScoreCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        => RedrawScoreGraph();

    private void RedrawScoreGraph()
    {
        var history = _vm.ScoreHistory;
        var w = ScoreCanvas.ActualWidth;
        var h = ScoreCanvas.ActualHeight;
        if (w <= 0 || h <= 0 || history.Count == 0) return;

        double xScale = history.Count > 1 ? w / (history.Count - 1) : w;
        double yScale = h / 64.0;

        // 中央線（石数 32 の位置）
        double midY = h - 32 * yScale;
        MidLine.X1 = 0; MidLine.X2 = w;
        MidLine.Y1 = midY; MidLine.Y2 = midY;

        // 黒・白ラインの点を構築
        var blackPoints = new PointCollection(history.Count);
        var whitePoints = new PointCollection(history.Count);
        for (int i = 0; i < history.Count; i++)
        {
            double x = history.Count > 1 ? i * xScale : 0;
            blackPoints.Add(new Point(x, h - history[i].BlackCount * yScale));
            whitePoints.Add(new Point(x, h - history[i].WhiteCount * yScale));
        }
        BlackScoreLine.Points = blackPoints;
        WhiteScoreLine.Points = whitePoints;

        // 現在手縦線
        double cx = history.Count > 1 ? (history.Count - 1) * xScale : 0;
        CurrentMoveLine.X1 = cx; CurrentMoveLine.X2 = cx;
        CurrentMoveLine.Y1 = 0;  CurrentMoveLine.Y2 = h;
    }

    private void OnTimeLimitSecondsLostFocus(object sender, RoutedEventArgs e)
    {
        // TextBox の値をバインディング更新してから設定を保存する
        TimeLimitSecondsBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        _vm.SaveTimeLimitSettings();
    }

    private void OnTimeLimitSecondsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TimeLimitSecondsBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            _vm.SaveTimeLimitSettings();
            e.Handled = true;
        }
    }

    private async void OnSaveKifu(object sender, RoutedEventArgs e)
    {
        var record = _vm.LastKifuRecord;
        if (record is null)
        {
            MessageBox.Show("保存できる棋譜がありません。ゲームを終了させてください。",
                "棋譜を保存", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title            = "棋譜を保存",
            Filter           = "棋譜ファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
            DefaultExt       = ".json",
            FileName         = $"kifu_{record.PlayedAt.LocalDateTime:yyyyMMdd_HHmmss}.json",
            InitialDirectory = KifuSerializer.GetDefaultSaveDirectory(),
        };

        if (dlg.ShowDialog() == true)
        {
            await KifuSerializer.SaveAsync(record, dlg.FileName);
            MessageBox.Show($"棋譜を保存しました:\n{dlg.FileName}",
                "棋譜を保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void OnOpenKifu(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title            = "棋譜を開く",
            Filter           = "棋譜ファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
            InitialDirectory = KifuSerializer.GetDefaultSaveDirectory(),
        };

        if (dlg.ShowDialog() != true)
            return;

        var record = await KifuSerializer.LoadAsync(dlg.FileName);
        if (record is null)
        {
            MessageBox.Show("棋譜ファイルを読み込めませんでした。ファイルが正しい形式か確認してください。",
                "棋譜を開く", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var player = new KifuPlayer(record);
        var vm     = new KifuViewModel(player, record);
        var win    = new KifuWindow(vm) { Owner = this };
        win.ShowDialog();
    }
}
