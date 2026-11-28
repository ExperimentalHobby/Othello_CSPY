namespace Technopro.Othello.Tests.ViewModels;

using System.Collections.Concurrent;
using System.Diagnostics;
using Technopro.Othello.ViewModels;

/// <summary>
/// TurnTimerService の単体テスト（Issue #127: GameViewModel からのタイマー分離）。
/// UI・GameViewModel に依存しない純粋なカウントダウンタイマーとしての挙動を検証する。
/// </summary>
public class TurnTimerServiceTests
{
	/// <summary>
	/// 条件が真になるまでポーリングして待つ（固定 Task.Delay と異なり、CI 環境の負荷で
	/// 実際の所要時間が伸びても、上限内であればフレーキーにならない。Issue #128 で発生した
	/// Linux ランナーでのタイミング起因の失敗を受けて導入）。
	/// </summary>
	/// <param name="condition">真になるまで待つ条件</param>
	/// <param name="timeoutMs">この時間内に条件が真にならなければ諦めて抜ける（呼び出し元の Assert で失敗として検出される）</param>
	private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
	{
		var sw = Stopwatch.StartNew();
		while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
			await Task.Delay(20);
	}

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
	/// 固定時間の待機ではなく条件成立をポーリングすることで、CI 環境の負荷で
	/// 実時間が伸びてもフレーキーにならないようにする。
	/// パス条件: Start(2) 後、最新 Tick 値が 1 になること。
	/// </summary>
	[Fact]
	public async Task Start_TicksDownEverySecond()
	{
		var timer = new TurnTimerService();
		int? received = null;
		timer.Tick += remaining => received = remaining;

		timer.Start(2);
		await WaitUntilAsync(() => received == 1);

		Assert.Equal(1, received);
	}

	/// <summary>
	/// 残り時間が 0 に到達すると Expired が発火することを確認する。
	/// 固定時間の待機ではなく条件成立をポーリングすることで、CI 環境の負荷で
	/// 実時間が伸びてもフレーキーにならないようにする。
	/// パス条件: Start(1) 後、Expired が発火すること。
	/// </summary>
	[Fact]
	public async Task Start_WhenDurationElapses_FiresExpired()
	{
		var timer = new TurnTimerService();
		bool expired = false;
		timer.Expired += () => expired = true;

		timer.Start(1);
		await WaitUntilAsync(() => expired);

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

		// 「発火しないこと」の確認のため、Expired 未発火を待ち続けるポーリングは使えない。
		// Start(5) に対して十分短い（1/5 未満の）固定待機で「発火していないこと」を確認する。
		await Task.Delay(1000);

		Assert.False(expired);
	}

	/// <summary>
	/// Post された継続を即時実行せず保持する SynchronizationContext。
	/// 「待機は完了したが、継続はまだ実行されていない」状態をテストで作るために使う。
	/// </summary>
	private sealed class QueuedSynchronizationContext : SynchronizationContext
	{
		private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

		public bool HasPending => !_queue.IsEmpty;

		public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

		public void RunPending()
		{
			while (_queue.TryDequeue(out var item))
				item.Callback(item.State);
		}
	}

	/// <summary>
	/// 待機完了後、継続が実行される前に Stop した場合、古いタイマーが通知しないことを確認する（Issue #260）。
	/// パス条件: Stop の Tick(0) より後に Tick が発火せず、Expired も発火しないこと。
	/// </summary>
	[Fact]
	public void Stop_AfterDelayCompletedBeforeContinuation_DoesNotNotify()
	{
		var context = new QueuedSynchronizationContext();
		var original = SynchronizationContext.Current;
		var timer = new TurnTimerService();
		var ticks = new List<int>();
		bool expired = false;
		timer.Tick += ticks.Add;
		timer.Expired += () => expired = true;

		try
		{
			SynchronizationContext.SetSynchronizationContext(context);
			timer.Start(1);
			Assert.True(SpinWait.SpinUntil(() => context.HasPending, 5000), "待機が完了しませんでした");

			timer.Stop();
			context.RunPending();
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext(original);
		}

		// 先頭の 0 は Start 内部の Stop によるもの
		Assert.Equal(new[] { 0, 1, 0 }, ticks);
		Assert.False(expired);
	}

	/// <summary>
	/// Start を連続で呼ぶと、先に開始したタイマーはキャンセルされ Expired を発火しないことを確認する。
	/// パス条件: Start(1) 直後に Start(5) を呼び、Expired が発火していないこと。
	/// </summary>
	[Fact]
	public async Task Start_CalledAgain_CancelsPreviousTimer()
	{
		var timer = new TurnTimerService();
		bool expired = false;
		timer.Expired += () => expired = true;

		timer.Start(1);
		timer.Start(5);

		// 「発火しないこと」の確認のため、Expired 未発火を待ち続けるポーリングは使えない。
		// 2 回目の Start(5) に対して十分短い固定待機で「発火していないこと」を確認する。
		await Task.Delay(1000);

		Assert.False(expired);
	}
}
