using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WinUI3;

public sealed partial class KifuWindow : Window
{
    private readonly KifuViewModel _vm;

    public KifuWindow(KifuViewModel vm)
    {
        this.InitializeComponent();
        _vm = vm;

        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 800));

        if (this.Content is FrameworkElement root)
            root.DataContext = vm;
    }

    private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (BoardRepeater.Layout is not UniformGridLayout layout) return;

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
