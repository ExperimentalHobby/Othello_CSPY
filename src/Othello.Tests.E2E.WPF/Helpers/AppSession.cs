namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

/// <summary>
/// テスト 1 件分の Othello.WPF.exe 起動セッション。
/// <see cref="Launch"/> で起動し、<see cref="Dispose"/> でアプリを終了する。
/// </summary>
public sealed class AppSession : IDisposable
{
	/// <summary>起動からメインウィンドウ表示までの待機上限。Python プロセス起動を含むため余裕を持たせる。</summary>
	private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(30);

	/// <summary>ウィンドウを閉じてから正常終了を待つ上限。超えた場合は強制終了する。</summary>
	private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(10);

	private readonly Application _app;
	private readonly UIA3Automation _automation;
	private bool _disposed;

	private AppSession(Application app, UIA3Automation automation, Window mainWindow)
	{
		_app = app;
		_automation = automation;
		MainWindow = mainWindow;
	}

	/// <summary>メインウィンドウ。</summary>
	public Window MainWindow { get; }

	/// <summary>
	/// テストの出力ディレクトリにある Othello.WPF.exe を起動し、メインウィンドウが表示されるまで待つ。
	/// </summary>
	/// <exception cref="FileNotFoundException">exe が出力ディレクトリに存在しない場合。</exception>
	/// <exception cref="TimeoutException">待機上限までにメインウィンドウが表示されなかった場合。</exception>
	public static AppSession Launch()
	{
		var exePath = Path.Combine(AppContext.BaseDirectory, "Othello.WPF.exe");
		if (!File.Exists(exePath))
			throw new FileNotFoundException("起動対象の exe が見つかりません。", exePath);

		var app = Application.Launch(new ProcessStartInfo(exePath)
		{
			WorkingDirectory = AppContext.BaseDirectory,
		});
		app.CloseTimeout = CloseTimeout;
		var automation = new UIA3Automation();
		try
		{
			var mainWindow = app.GetMainWindow(automation, LaunchTimeout)
				?? throw new TimeoutException($"{LaunchTimeout.TotalSeconds} 秒以内にメインウィンドウが表示されませんでした。");
			return new AppSession(app, automation, mainWindow);
		}
		catch
		{
			// 起動途中で失敗してもプロセスを残さない
			app.Close(killIfCloseFails: true);
			app.Dispose();
			automation.Dispose();
			throw;
		}
	}

	/// <summary>
	/// アプリを終了する。ウィンドウを閉じて正常終了を待ち、<see cref="CloseTimeout"/> を超えた場合は強制終了する。
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;

		if (!_app.HasExited)
			_app.Close(killIfCloseFails: true);
		_app.Dispose();
		_automation.Dispose();
	}
}
