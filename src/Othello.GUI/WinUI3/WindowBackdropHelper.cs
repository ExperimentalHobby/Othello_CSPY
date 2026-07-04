using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace Technopro.Othello.WinUI3;

/// <summary>
/// WinUI3 の Window に Mica（非対応環境では Acrylic）の背景エフェクトを適用する。
/// MainWindow / KifuWindow の両方から呼び出される共通ロジックをここに集約する。
/// </summary>
internal static class WindowBackdropHelper
{
    /// <summary>
    /// 指定された Window に Mica を適用する。非対応環境（Windows 10 等）では Acrylic にフォールバックする。
    /// どちらも非対応の場合は何もしない（既存の Background 描画のまま）。
    /// </summary>
    public static void Apply(Window window)
    {
        if (window.Content is not FrameworkElement root) return;

        SystemBackdropConfiguration configurationSource = new()
        {
            IsInputActive = true,
        };

        ICompositionSupportsSystemBackdrop target = window.As<ICompositionSupportsSystemBackdrop>();

        ISystemBackdropControllerWithTargets? controller = MicaController.IsSupported()
            ? new MicaController()
            : DesktopAcrylicController.IsSupported()
                ? new DesktopAcrylicController()
                : null;

        if (controller is null) return;

        void UpdateTheme()
        {
            configurationSource.Theme = root.ActualTheme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default,
            };
        }

        void OnActivated(object sender, WindowActivatedEventArgs e)
            => configurationSource.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;

        void OnThemeChanged(FrameworkElement sender, object e) => UpdateTheme();

        void OnClosed(object sender, WindowEventArgs e)
        {
            controller.Dispose();
            window.Activated -= OnActivated;
            root.ActualThemeChanged -= OnThemeChanged;
            window.Closed -= OnClosed;
        }

        window.Activated += OnActivated;
        root.ActualThemeChanged += OnThemeChanged;
        window.Closed += OnClosed;

        UpdateTheme();
        controller.AddSystemBackdropTarget(target);
        controller.SetSystemBackdropConfiguration(configurationSource);
    }
}
