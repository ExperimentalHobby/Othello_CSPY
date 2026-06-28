using System.IO;
using System.Windows;
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

        Loaded  += async (_, _) => await _vm.StartNewGameAsync();
        Closed  += (_, _) => (_vm as IDisposable)?.Dispose();
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
