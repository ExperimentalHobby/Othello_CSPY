using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace Technopro.Othello.WinUI3;

internal static class Program
{
    [global::System.Runtime.InteropServices.DllImport("Microsoft.ui.xaml.dll")]
    [global::System.Runtime.InteropServices.DefaultDllImportSearchPaths(
        global::System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories)]
    private static extern void XamlCheckProcessRequirements();

    [global::System.Runtime.InteropServices.DllImport("user32.dll",
        CharSet = global::System.Runtime.InteropServices.CharSet.Unicode,
        EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [global::System.STAThread]
    static int Main(string[] args)
    {
        // Windows App Runtime を初期化（ShowUI なし → 失敗時は独自メッセージ）
        // [ModuleInitializer] の自動初期化は WindowsAppSdkBootstrapInitialize=false で無効化済み
        int hr = 0;
        bool ok = Bootstrap.TryInitialize(
            0x00020000u,                             // version 2.0
            "",                                      // versionTag（空 = stable）
            new PackageVersion(0ul),                 // minVersion（0 = 任意）
            Bootstrap.InitializeOptions.None,        // ShowUI を使わない
            out hr);

        if (!ok)
        {
            MessageBox(
                nint.Zero,
                "Windows App Runtime 2.0 が見つかりません。\n\n" +
                "以下のコマンドを実行してインストールしてください：\n\n" +
                "    winget install Microsoft.WindowsAppRuntime.2.0\n\n" +
                $"エラーコード: 0x{hr:X8}",
                "Othello - 起動エラー",
                0x10u /* MB_ICONERROR */);
            return hr;
        }

        // WinUI3 の起動（ランタイム初期化後に JIT されるよう分離）
        Launch();
        return 0;
    }

    // NoInlining: WinUI3 型の JIT は Bootstrap.TryInitialize の後に行われるよう分離
    [global::System.Runtime.CompilerServices.MethodImpl(
        global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Launch()
    {
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        XamlCheckProcessRequirements();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
