using System.Windows;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WPF;

public partial class KifuWindow : Window
{
    public KifuWindow(KifuViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
