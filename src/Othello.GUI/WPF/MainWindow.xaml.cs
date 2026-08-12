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
        var w = ScoreCanvas.ActualWidth;
        var h = ScoreCanvas.ActualHeight;

        // 座標計算本体は ScoreGraphCalculator（WPF/WinUI3 共通）に委譲する（Issue #128）。
        // このメソッドは計算結果を WPF 描画用の型（Point/PointCollection）に変換するだけ。
        var result = ScoreGraphCalculator.Calculate(_vm.ScoreHistory, w, h);
        if (result is not { } r) return;

        // 中央線（石数 32 の位置）
        MidLine.X1 = 0; MidLine.X2 = w;
        MidLine.Y1 = r.MidLineY; MidLine.Y2 = r.MidLineY;

        // 黒・白ラインの点を構築
        var blackPoints = new PointCollection(r.BlackPoints.Count);
        var whitePoints = new PointCollection(r.WhitePoints.Count);
        for (int i = 0; i < r.BlackPoints.Count; i++)
        {
            blackPoints.Add(new Point(r.BlackPoints[i].X, r.BlackPoints[i].Y));
            whitePoints.Add(new Point(r.WhitePoints[i].X, r.WhitePoints[i].Y));
        }
        BlackScoreLine.Points = blackPoints;
        WhiteScoreLine.Points = whitePoints;

        // 現在手縦線
        CurrentMoveLine.X1 = r.CurrentMoveX; CurrentMoveLine.X2 = r.CurrentMoveX;
        CurrentMoveLine.Y1 = 0;              CurrentMoveLine.Y2 = h;
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
            try
            {
                await KifuSerializer.SaveAsync(record, dlg.FileName);
                MessageBox.Show($"棋譜を保存しました:\n{dlg.FileName}",
                    "棋譜を保存", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show($"棋譜の保存に失敗しました:\n{ex.Message}",
                    "棋譜を保存", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        KifuRecord? record;
        try
        {
            record = await KifuSerializer.LoadAsync(dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"棋譜ファイルの読み込みに失敗しました:\n{ex.Message}",
                "棋譜を開く", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

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
