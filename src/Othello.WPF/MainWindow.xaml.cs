using System.Windows;
using Technopro.Othello.WPF.ViewModels;

namespace Technopro.Othello.WPF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new GameViewModel();
    }
}
