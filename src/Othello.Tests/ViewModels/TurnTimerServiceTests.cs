namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.ViewModels;

/// <summary>
/// TurnTimerService の単体テスト（Issue #127: GameViewModel からのタイマー分離）。
/// UI・GameViewModel に依存しない純粋なカウントダウンタイマーとしての挙動を検証する。
/// </summary>
public class TurnTimerServiceTests
{
	/// <summary>
	/// Start 呼び出し直後（最初の await 前）に、指定した秒数で Tick が同期的に発火することを確認する。
	/// パス条件: Start(5) 呼び出し直後に tick 引数が 5 であること。
	/// </summary>
	[Fact]
	public void Start_FiresTickImmediatelyWithDuration()
	{
		var timer = new TurnTimerService();
		int? received = null;
		timer.Tick += remaining => received = remaining;

		timer.Start(5);

		Assert.Equal(5, received);
	}

	/// <summary>
	/// 1 秒経過ごとに Tick がデクリメントして発火することを確認する。
	/// パス条件: Start(2) から約 1.1 秒後の最新 Tick 値が 1 であること。
	/// </summary>
	[Fact]
	public async Task Start_TicksDownEverySecond()
	{
		var timer = new TurnTimerService();
		int? received = null;
		timer.Tick += remaining => received = remaining;

		timer.Start(2);
		await Task.Delay(1100);

		Assert.Equal(1, received);
	}

	/// <summary>
	/// 残り時間が 0 に到達すると Expired が発火することを確認する。
	/// パス条件: Start(1) から約 1.1 秒後に Expired が発火していること。
	/// </summary>
	[Fact]
	public async Task Start_WhenDurationElapses_FiresExpired()
	{
		var timer = new TurnTimerService();
		bool expired = false;
		timer.Expired += () => expired = true;

		timer.Start(1);
		await Task.Delay(1100);

		Assert.True(expired);
	}

	/// <summary>
	/// Stop を呼ぶと即座に Tick(0) が発火し、以降は Expired が発火しないことを確認する。
	/// パス条件: Stop 直後の tick が 0 であり、待機後も Expired が発火していないこと。
	/// </summary>
	[Fact]
	public async Task Stop_FiresTickZero_AndPreventsExpired()
	{
		var timer = new TurnTimerService();
		int? received = null;
		bool expired = false;
		timer.Tick += remaining => received = remaining;
		timer.Expired += () => expired = true;

		timer.Start(5);
		timer.Stop();
		Assert.Equal(0, received);

		await Task.Delay(1100);

		Assert.False(expired);
	}

	/// <summary>
	/// Start を連続で呼ぶと、先に開始したタイマーはキャンセルされ Expired を発火しないことを確認する。
	/// パス条件: Start(1) 直後に Start(5) を呼び、1 秒後の時点で Expired が発火していないこと。
	/// </summary>
	[Fact]
	public async Task Start_CalledAgain_CancelsPreviousTimer()
	{
		var timer = new TurnTimerService();
		bool expired = false;
		timer.Expired += () => expired = true;

		timer.Start(1);
		timer.Start(5);
		await Task.Delay(1100);

		Assert.False(expired);
	}
}
