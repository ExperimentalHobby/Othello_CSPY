namespace Technopro.Othello.Tests.E2E.WPF;

using Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>WPF 版アプリの起動に関する GUI テスト。</summary>
public class LaunchTests
{
	/// <summary>
	/// アプリを起動するとメインウィンドウが表示されることを確認する。
	/// パス条件: メインウィンドウが取得でき、タイトルが "Othello (オセロ) - WPF" であること。
	/// </summary>
	[UiFact]
	public void Launch_ShowsMainWindowWithTitle()
	{
		using var session = AppSession.Launch();

		Assert.Equal("Othello (オセロ) - WPF", session.MainWindow.Title);
	}
}
