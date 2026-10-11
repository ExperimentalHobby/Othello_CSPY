namespace Technopro.Othello.Tests.Helpers;

using System.Runtime.CompilerServices;
using Technopro.Othello.Core.Settings;
using Technopro.Othello.Core.Stats;

/// <summary>
/// テストアセンブリのロード時に、統計・設定ファイルの既定保存先を一時ディレクトリへ切り替える（Issue #216）。
/// <c>new GameViewModel(...)</c> が <c>statsRepository</c> / <c>settingsFilePath</c> を省略しても、
/// 開発者の実データ（%LOCALAPPDATA%\OthelloCspy）を読み書きしないようにする。
/// </summary>
internal static class TestEnvironmentIsolation
{
	/// <summary>テストプロセス専用の既定保存先ディレクトリ。</summary>
	internal static string IsolatedDirectory { get; } = Path.Combine(
		Path.GetTempPath(), "OthelloCspyTests", Environment.ProcessId.ToString());

	/// <summary>テストアセンブリのロード時に一度だけ実行され、既定パスを差し替える。</summary>
	[ModuleInitializer]
	internal static void Initialize()
	{
		StatsRepository.DefaultFilePath = Path.Combine(IsolatedDirectory, "stats.json");
		OthelloSettingsManager.DefaultFilePath = Path.Combine(IsolatedDirectory, "settings.json");
	}
}
