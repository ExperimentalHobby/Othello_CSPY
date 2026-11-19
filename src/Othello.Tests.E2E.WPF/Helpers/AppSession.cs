namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
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

	/// <summary>要素が見つかるまでの待機上限。</summary>
	private static readonly TimeSpan ElementTimeout = TimeSpan.FromSeconds(10);

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
		// UIA3Automation の生成（COM 初期化）も失敗しうるため、起動済みの app を閉じられるよう try 内で生成する
		UIA3Automation? automation = null;
		try
		{
			app.CloseTimeout = CloseTimeout;
			automation = new UIA3Automation();
			var mainWindow = app.GetMainWindow(automation, LaunchTimeout)
				?? throw new TimeoutException($"{LaunchTimeout.TotalSeconds} 秒以内にメインウィンドウが表示されませんでした。");
			return new AppSession(app, automation, mainWindow);
		}
		catch
		{
			// 起動途中で失敗してもプロセスを残さない（終了処理自体が失敗しても automation の解放は行う）
			try
			{
				app.Close(killIfCloseFails: true);
			}
			finally
			{
				app.Dispose();
				automation?.Dispose();
			}
			throw;
		}
	}

	/// <summary>
	/// AutomationId で要素を取得する。表示直後で見つからない場合は <see cref="ElementTimeout"/> まで再試行する。
	/// </summary>
	/// <param name="automationId">取得する要素の AutomationId（マスは "d4" などの盤面表記）。</param>
	/// <exception cref="TimeoutException">待機上限までに要素が見つからなかった場合。</exception>
	public AutomationElement GetElement(string automationId)
	{
		var result = Retry.WhileNull(
			() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
			ElementTimeout);
		return result.Result
			?? throw new TimeoutException($"{ElementTimeout.TotalSeconds} 秒以内に AutomationId \"{automationId}\" の要素が見つかりませんでした。");
	}

	/// <summary>
	/// アプリを終了する。ウィンドウを閉じて正常終了を待ち、<see cref="CloseTimeout"/> を超えた場合は強制終了する。
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;

		// Close / Dispose のいずれかが例外を出しても、後続の解放（プロセスハンドル・Automation）は必ず実行する。
		// _disposed は既に true のため、ここで取りこぼすと再度 Dispose しても解放されない。
		try
		{
			if (!_app.HasExited)
				_app.Close(killIfCloseFails: true);
		}
		finally
		{
			try
			{
				_app.Dispose();
			}
			finally
			{
				_automation.Dispose();
			}
		}
	}
}
