using System.Windows;
using Technopro.Othello.WPF.ViewModels;

namespace Technopro.Othello.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new GameViewModel();

        // ウィンドウ閉鎖時に ViewModel を破棄し、Python プロセスを確実に終了させる
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
