using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Technopro.Othello.WinUI3;

public partial class App : Application
{
	private Window? _window;

	public App()
	{
		this.InitializeComponent();
		// 未処理例外: クラッシュダイアログの代わりにメッセージを表示してからアプリを終了する（黙って継続しない）。
		this.UnhandledException += OnUnhandledException;
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow();
		_window.Activate();
	}

	private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		// 既定のクラッシュ処理を抑止し、こちらでメッセージを表示してから終了する。
		e.Handled = true;
		_ = ShowErrorAndExitAsync(e.Exception);
	}

	private async Task ShowErrorAndExitAsync(Exception ex)
	{
		if (_window?.Content is FrameworkElement root)
		{
			var dlg = new ContentDialog
			{
				Title = "エラー",
				Content = $"予期しないエラーが発生したため、アプリケーションを終了します。\n\n{ex.Message}",
				CloseButtonText = "OK",
				XamlRoot = root.XamlRoot,
			};
			try
			{
				await dlg.ShowAsync();
			}
			catch
			{
				// ダイアログ表示自体に失敗しても終了処理は継続する。
			}
		}
		Exit();
	}
}
