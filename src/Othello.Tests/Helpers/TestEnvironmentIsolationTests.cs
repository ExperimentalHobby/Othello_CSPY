namespace Technopro.Othello.Tests.Helpers;

using Technopro.Othello.Core.Settings;
using Technopro.Othello.Core.Stats;

/// <summary>テスト実行が開発者の実データを参照しないことの検証（Issue #216）。</summary>
public class TestEnvironmentIsolationTests
{
	private static readonly string RealDirectory = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OthelloCspy");

	/// <summary>
	/// 統計の既定保存先が実データのディレクトリ配下でないことを確認する。
	/// パス条件: StatsRepository.DefaultFilePath が %LOCALAPPDATA%\OthelloCspy 配下にならないこと。
	/// </summary>
	[Fact]
	public void StatsRepository_DefaultFilePath_IsNotUnderRealUserData() =>
		Assert.False(StatsRepository.DefaultFilePath.StartsWith(RealDirectory, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// 設定の既定保存先が実データのディレクトリ配下でないことを確認する。
	/// パス条件: OthelloSettingsManager.DefaultFilePath が %LOCALAPPDATA%\OthelloCspy 配下にならないこと。
	/// </summary>
	[Fact]
	public void SettingsManager_DefaultFilePath_IsNotUnderRealUserData() =>
		Assert.False(OthelloSettingsManager.DefaultFilePath.StartsWith(RealDirectory, StringComparison.OrdinalIgnoreCase));
}
