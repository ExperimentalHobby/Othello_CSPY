namespace Technopro.Othello.ViewModels;

/// <summary>
/// 1 秒ごとにカウントダウンするタイマー（Issue #127: GameViewModel からのタイマー機能分離）。
/// UI・ゲーム状態に依存しない自己完結したロジックとし、<see cref="Tick"/> / <see cref="Expired"/>
/// イベントで呼び出し元（GameViewModel）に通知する。
/// </summary>
public class TurnTimerService : IDisposable
{
	private CancellationTokenSource? _cts;

	/// <summary>
	/// 残り秒数が変化するたびに発火する（Start 直後・毎秒のデクリメント・Stop 時の 0 リセット）。
	/// </summary>
	public event Action<int>? Tick;

	/// <summary>
	/// カウントダウンが Stop でキャンセルされず、自然に残り 0 秒へ到達した（時間切れ）ときに発火する。
	/// </summary>
	public event Action? Expired;

	/// <summary>
	/// durationSeconds 秒からカウントダウンを開始する。
	/// 既に実行中のタイマーがあれば、まず <see cref="Stop"/> と同じ手順で停止してから開始する。
	/// </summary>
	/// <param name="durationSeconds">カウントダウンする秒数</param>
	public void Start(int durationSeconds)
	{
		Stop();

		var cts = new CancellationTokenSource();
		_cts = cts;
		_ = RunAsync(durationSeconds, cts.Token);
	}

	/// <summary>
	/// タイマーを停止し、残り秒数を 0 として Tick を発火する。
	/// 実行中のタイマーがなくても安全に呼び出せる。
	/// </summary>
	public void Stop()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
		Tick?.Invoke(0);
	}

	/// <summary>
	/// カウントダウン本体。1 秒ごとに Tick を発火し、キャンセルされずに 0 秒へ到達したら Expired を発火する。
	/// </summary>
	private async Task RunAsync(int durationSeconds, CancellationToken ct)
	{
		int remaining = durationSeconds;
		Tick?.Invoke(remaining);

		try
		{
			while (remaining > 0)
			{
				await Task.Delay(1000, ct);
				remaining--;
				Tick?.Invoke(remaining);
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}

		Expired?.Invoke();
	}

	/// <summary>保持している CancellationTokenSource を破棄する。</summary>
	public void Dispose()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		GC.SuppressFinalize(this);
	}
}
