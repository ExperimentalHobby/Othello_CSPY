using System.Windows;
using System.Windows.Threading;

namespace Technopro.Othello.WPF;

public partial class App : Application
{
    public App()
    {
        // UI スレッドの未処理例外: メッセージを表示してからアプリを終了する（黙って継続しない）。
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        // UI スレッド以外（バックグラウンドスレッド）の未処理例外。IsTerminating は基本的に true。
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"予期しないエラーが発生したため、アプリケーションを終了します。\n\n{e.Exception.Message}",
            "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"予期しないエラーが発生したため、アプリケーションを終了します。\n\n{ex.Message}",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // IsTerminating はほぼ常に true であり、このハンドラの後プロセスは終了する。
    }
}
