namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

using FlaUI.Core.Tools;

/// <summary>GUI テスト共通の条件待機ヘルパー。<c>Thread.Sleep</c> を使わず、条件が満たされるまでポーリングする。</summary>
public static class UiWait
{
	/// <summary>待機の既定上限。</summary>
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	/// <summary>条件が満たされるまで最大 10 秒ポーリングし、満たされなければテストを失敗させる。</summary>
	/// <param name="condition">待機する条件。</param>
	/// <param name="failureMessage">タイムアウト時に評価する失敗メッセージ（最終状態を含められるよう遅延評価）。</param>
	public static void Until(Func<bool> condition, Func<string> failureMessage)
	{
		var result = Retry.WhileFalse(condition, DefaultTimeout);
		Assert.True(result.Result, failureMessage());
	}
}
