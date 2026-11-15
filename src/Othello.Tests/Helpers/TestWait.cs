namespace Technopro.Othello.Tests.Helpers;

/// <summary>
/// 非同期処理の完了を「固定時間 Sleep」ではなく「条件が成立するまでポーリング」で待つヘルパー（Issue #222）。
/// CI 環境の負荷で所要時間が伸びても、上限内であればフレーキーにならない。
/// 上限内に条件が成立しなければ例外で確実に失敗させる。
/// </summary>
public static class TestWait
{
	/// <summary>条件成立を待つ既定の上限時間。</summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	/// <summary>
	/// 条件が真になるまで待つ（同期版）。
	/// パス条件: 上限内に <paramref name="condition"/> が真になれば戻る。
	/// </summary>
	/// <param name="condition">真になるまで待つ条件。</param>
	/// <param name="failureMessage">タイムアウト時の失敗メッセージ（何を待っていたか）。</param>
	/// <param name="timeout">上限時間。省略時は <see cref="DefaultTimeout"/>。</param>
	public static void Until(Func<bool> condition, string failureMessage, TimeSpan? timeout = null)
		=> Assert.True(SpinWait.SpinUntil(condition, timeout ?? DefaultTimeout), failureMessage);

	/// <summary>
	/// 条件が真になるまで待つ（非同期版。待機中にスレッドをブロックしない）。
	/// パス条件: 上限内に <paramref name="condition"/> が真になれば戻る。
	/// </summary>
	/// <param name="condition">真になるまで待つ条件。</param>
	/// <param name="failureMessage">タイムアウト時の失敗メッセージ（何を待っていたか）。</param>
	/// <param name="timeout">上限時間。省略時は <see cref="DefaultTimeout"/>。</param>
	public static async Task UntilAsync(Func<bool> condition, string failureMessage, TimeSpan? timeout = null)
	{
		var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
		while (!condition() && DateTime.UtcNow < deadline)
			await Task.Delay(10);
		Assert.True(condition(), failureMessage);
	}
}
