using System.Windows;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new GameViewModel();
        DataContext = vm;

        // Loaded 後に非同期でゲーム開始することでウィンドウ表示をブロックしない
        Loaded += async (_, _) => await vm.StartNewGameAsync();

        // ウィンドウ閉鎖時に ViewModel を破棄し、Python プロセスを確実に終了させる
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
