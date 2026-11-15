namespace Technopro.Othello.Tests.Helpers;

/// <summary><see cref="TestWait"/> の単体テスト。</summary>
public class TestWaitTests
{
	/// <summary>
	/// 途中で条件が真になる場合、真になった時点で戻ることを確認する。
	/// パス条件: 別スレッドが 50ms 後にフラグを立てた後、Until が例外なく戻りフラグが true であること。
	/// </summary>
	[Fact]
	public void Until_ConditionBecomesTrue_Returns()
	{
		var flag = false;
		_ = Task.Run(async () => { await Task.Delay(50); Volatile.Write(ref flag, true); });

		TestWait.Until(() => Volatile.Read(ref flag), "flag が true になること", TimeSpan.FromSeconds(5));

		Assert.True(Volatile.Read(ref flag));
	}

	/// <summary>
	/// 条件が上限内に真にならない場合、失敗メッセージ付きで例外が送出されることを確認する。
	/// パス条件: 常に false の条件で、例外メッセージに指定した失敗メッセージが含まれること。
	/// </summary>
	[Fact]
	public void Until_ConditionNeverTrue_ThrowsWithFailureMessage()
	{
		var ex = Record.Exception(() =>
			TestWait.Until(() => false, "永遠に成立しない条件", TimeSpan.FromMilliseconds(100)));

		Assert.NotNull(ex);
		Assert.Contains("永遠に成立しない条件", ex!.Message);
	}

	/// <summary>
	/// 非同期版でも、途中で条件が真になれば真になった時点で戻ることを確認する。
	/// パス条件: 別タスクが 50ms 後にフラグを立てた後、UntilAsync が例外なく戻りフラグが true であること。
	/// </summary>
	[Fact]
	public async Task UntilAsync_ConditionBecomesTrue_Returns()
	{
		var flag = false;
		_ = Task.Run(async () => { await Task.Delay(50); Volatile.Write(ref flag, true); });

		await TestWait.UntilAsync(() => Volatile.Read(ref flag), "flag が true になること", TimeSpan.FromSeconds(5));

		Assert.True(Volatile.Read(ref flag));
	}

	/// <summary>
	/// 非同期版でも、条件が上限内に真にならなければ失敗メッセージ付きで例外が送出されることを確認する。
	/// パス条件: 常に false の条件で、例外メッセージに指定した失敗メッセージが含まれること。
	/// </summary>
	[Fact]
	public async Task UntilAsync_ConditionNeverTrue_ThrowsWithFailureMessage()
	{
		var ex = await Record.ExceptionAsync(() =>
			TestWait.UntilAsync(() => false, "永遠に成立しない条件", TimeSpan.FromMilliseconds(100)));

		Assert.NotNull(ex);
		Assert.Contains("永遠に成立しない条件", ex!.Message);
	}
}
