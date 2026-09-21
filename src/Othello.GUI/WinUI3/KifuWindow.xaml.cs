using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Technopro.Othello.ViewModels;

namespace Technopro.Othello.WinUI3;

public sealed partial class KifuWindow : Window
{
	public KifuWindow(KifuViewModel vm)
	{
		this.InitializeComponent();
		WindowBackdropHelper.Apply(this);

		AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 800));

		if (this.Content is FrameworkElement root)
			root.DataContext = vm;
	}

	private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (BoardRepeater.Layout is UniformGridLayout layout)
			BoardLayoutHelper.UpdateCellSize(layout, e.NewSize);
	}
}
