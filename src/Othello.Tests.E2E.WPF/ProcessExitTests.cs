namespace Technopro.Othello.Tests.E2E.WPF;

using Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>アプリ終了時のプロセス後始末に関する GUI テスト（Python サブプロセスが残らないこと）。</summary>
public class ProcessExitTests
{
	/// <summary>ウィンドウを閉じてからアプリが自力で終了するまでの待機上限。</summary>
	private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

	/// <summary>
	/// ウィンドウを閉じるとアプリが終了し、Python AI を含む子孫プロセスも残らないことを確認する。
	/// パス条件: 閉じる前に Python AI の子孫プロセスが存在し、ウィンドウを閉じた後に exe が終了して、
	/// 記録しておいた子孫プロセスがすべて消えていること（強制終了には頼らない）。
	/// </summary>
	[UiFact]
	public void CloseWindow_TerminatesAppAndDescendantProcesses()
	{
		using var session = AppSession.Launch();

		// Python 連携の AI が起動するまで待つ（C# AI にフォールバックすると子孫がなく、この検証が成り立たない）
		var badge = session.GetElement("AiEngineBadge");
		UiWait.Until(
			() => AiBadge.IsPythonBacked(badge.Name),
			() => $"AI が Python 連携になりませんでした: \"{badge.Name}\"");

		// 親が終了すると親子関係を辿れなくなるため、閉じる前に子孫を記録する
		var descendants = ProcessTree.Descendants(session.ProcessId);
		Assert.NotEmpty(descendants);

		Assert.True(
			session.CloseWindow(ExitTimeout),
			$"{ExitTimeout.TotalSeconds} 秒以内にアプリが終了しませんでした。");

		UiWait.Until(
			() => descendants.All(p => !ProcessTree.IsRunning(p)),
			() => $"子孫プロセスが残っています: {string.Join(", ", descendants.Where(ProcessTree.IsRunning))}");
	}
}
