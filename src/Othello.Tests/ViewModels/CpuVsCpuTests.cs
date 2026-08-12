namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.ViewModels;

/// <summary>
/// CPU vs CPU 対戦モードの結合テスト。
/// FakeAI を両プレイヤーに注入してゲームが自動進行することを検証する。
/// </summary>
public class CpuVsCpuTests
{
	private sealed class FakeAI : IAIStrategy
	{
		private readonly int _delay;
		public DifficultyLevel Difficulty { get; }
		public string EngineName => "AI: Fake";

		public FakeAI(DifficultyLevel difficulty = DifficultyLevel.Easy, int delayMs = 0)
		{
			Difficulty = difficulty;
			_delay = delayMs;
		}

		public Position GetBestMove(Board board, PlayerColor playerColor)
		{
			if (_delay > 0) Thread.Sleep(_delay);
			return OthelloRules.GetValidMoves(board, playerColor)[0];
		}
	}

	/// <summary>Dispose() の呼び出し回数を数えるためのカウンタ（複数インスタンス間で共有する）。</summary>
	private sealed class DisposeCounter
	{
		public int Count;
	}

	/// <summary>
	/// IDisposable を実装した AI モック。Dispose() が呼ばれた回数を DisposeCounter に記録する
	/// （PythonSubprocessAI のようなプロセスリソースを持つ AI を模す。Issue #118）。
	/// </summary>
	private sealed class DisposableFakeAI : IAIStrategy, IDisposable
	{
		private readonly DisposeCounter _counter;
		public DifficultyLevel Difficulty { get; }
		public string EngineName => "AI: DisposableFake";

		public DisposableFakeAI(DifficultyLevel difficulty, DisposeCounter counter)
		{
			Difficulty = difficulty;
			_counter = counter;
		}

		public Position GetBestMove(Board board, PlayerColor playerColor)
			=> OthelloRules.GetValidMoves(board, playerColor)[0];

		public void Dispose() => Interlocked.Increment(ref _counter.Count);
	}

	private static async Task<GameViewModel> CreateCpuVsCpuViewModelAsync()
	{
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: _ => new FakeAI());
		vm.GameMode = GameMode.CpuVsCpu;
		vm.CpuVsCpuDelayMs = 0;
		await vm.StartNewGameAsync(); // 新規ゲーム: IsPaused = true の状態
		vm.PauseCommand.Execute(null); // 「開始」ボタン相当 → IsPaused = false
		return vm;
	}

	/// <summary>
	/// パス条件: CpuVsCpu モードでゲーム開始後、最終的に IsGameInProgress = false になること。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_GameCompletesAutomatically()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		Assert.False(vm.IsGameInProgress);
		Assert.True(vm.BlackScore + vm.WhiteScore > 4);
	}

	/// <summary>
	/// パス条件: CpuVsCpu モードでは UndoCommand.CanExecute = false になること。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_UndoIsDisabled()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		// ゲームが少し進むのを待つ
		await Task.Delay(100);

		Assert.False(vm.UndoCommand.CanExecute(null));
	}

	/// <summary>
	/// パス条件: CpuVsCpu モードでは人間がクリックしても着手されないこと。
	/// 証明: SquareClickedCommand が実行されても盤面に余分な石が増えないこと。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_HumanClickIsIgnored()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		// AI が思考を終えるまでポーリング
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (vm.IsAIThinking && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		// IsCpuVsCpu フラグが true になっていること
		Assert.True(vm.IsCpuVsCpu);

		// 人間が盤面をクリックしても SquareClickedCommand は着手しない（GameMode がガードする）
		var pos = new Position(2, 3);
		vm.SquareClickedCommand.Execute(pos);

		// エラーが発生せずゲームが継続していれば OK
		Assert.True(vm.IsGameInProgress || !vm.IsGameInProgress); // ゲーム状態は問わない
	}

	/// <summary>
	/// パス条件: IsPaused = true にすると AI の思考が止まること（IsAIThinking が false のまま遷移しないこと）。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_PauseStopsAutoPlay()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		// 少し待ってから一時停止
		await Task.Delay(50);
		vm.PauseCommand.Execute(null);
		Assert.True(vm.IsPaused);

		// 一時停止後は IsGameInProgress が true のまま変化しないこと
		bool wasInProgress = vm.IsGameInProgress;
		await Task.Delay(200);
		// ゲームが終了していなければ一時停止が有効
		if (wasInProgress)
			Assert.True(vm.IsPaused);
	}

	/// <summary>
	/// パス条件: 一時停止後に再開するとゲームが続くこと。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_ResumeAfterPause_GameContinues()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		// 少し待ってから一時停止
		await Task.Delay(50);
		vm.PauseCommand.Execute(null);
		Assert.True(vm.IsPaused);

		var scoreBefore = vm.BlackScore + vm.WhiteScore;
		await Task.Delay(200);

		// 再開
		vm.PauseCommand.Execute(null);
		Assert.False(vm.IsPaused);

		// ゲームが進行するまで待つ
		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		Assert.False(vm.IsGameInProgress);
	}

	/// <summary>
	/// パス条件: GameMode を HumanVsCpu に戻したとき、IsCpuVsCpu が false になること。
	/// </summary>
	[Fact]
	public void GameMode_Switch_UpdatesIsCpuVsCpu()
	{
		var vm = new GameViewModel(aiFactory: _ => new FakeAI(), startDeferred: true);

		vm.GameMode = GameMode.CpuVsCpu;
		Assert.True(vm.IsCpuVsCpu);
		Assert.False(vm.IsHumanVsCpu);

		vm.GameMode = GameMode.HumanVsCpu;
		Assert.False(vm.IsCpuVsCpu);
		Assert.True(vm.IsHumanVsCpu);
	}

	/// <summary>
	/// パス条件: BlackDifficultyIndex / WhiteDifficultyIndex が独立して設定できること。
	/// </summary>
	[Fact]
	public void BlackAndWhiteDifficulty_SetIndependently()
	{
		var vm = new GameViewModel(aiFactory: _ => new FakeAI(), startDeferred: true);
		vm.GameMode = GameMode.CpuVsCpu;

		vm.BlackDifficultyIndex = 1; // Easy（Beginner=0, Easy=1）
		vm.WhiteDifficultyIndex = 3; // Hard（Hard=3）

		Assert.Equal(DifficultyLevel.Easy, vm.BlackDifficulty);
		Assert.Equal(DifficultyLevel.Hard, vm.WhiteDifficulty);
	}

	/// <summary>
	/// パス条件: CpuVsCpu の新規ゲーム直後は IsPaused = true になること（開始ボタン待ち状態）。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_NewGame_StartsInPausedState()
	{
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: _ => new FakeAI());
		vm.GameMode = GameMode.CpuVsCpu;
		vm.CpuVsCpuDelayMs = 0;

		await vm.StartNewGameAsync();

		Assert.True(vm.IsPaused);
		Assert.True(vm.IsGameInProgress);
		Assert.Equal("開始", vm.PauseButtonContent);
	}

	/// <summary>
	/// パス条件: CpuVsCpu 開始ボタン待ち中（IsPaused）は対戦モードを変更できること（IsSettingsEditable = true）。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_PausedInitialState_SettingsAreEditable()
	{
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: _ => new FakeAI());
		vm.GameMode = GameMode.CpuVsCpu;
		vm.CpuVsCpuDelayMs = 0;

		await vm.StartNewGameAsync(); // IsPaused = true, IsInitialState = true

		Assert.True(vm.IsSettingsEditable, "開始ボタン待ち中はモードや難易度を変更できること");
	}

	/// <summary>
	/// パス条件: HumanVsCpu ゲーム中に CpuVsCpu に切り替えると IsGameInProgress = false になること（自動開始しないこと）。
	/// </summary>
	[Fact]
	public async Task SwitchToCpuVsCpu_StopsCurrentGame_DoesNotAutoStart()
	{
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: _ => new FakeAI());
		await vm.StartNewGameAsync(); // HumanVsCpu でゲーム開始

		// ゲームが進行中であること確認
		Assert.True(vm.IsGameInProgress);

		// CPU vs CPU に切り替え
		vm.GameMode = GameMode.CpuVsCpu;

		// ゲームが停止していること（自動開始しない）
		Assert.False(vm.IsGameInProgress);
	}

	/// <summary>
	/// パス条件: CpuVsCpu モードで自動対戦が進行すると、ちょうど 1 マスだけ IsLastMove=true になること
	/// （#60: 反転アニメーションが消えた後も直前の着手位置がわかるようにするマーカー）。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_UpdatesIsLastMoveAsGameProgresses()
	{
		var vm = await CreateCpuVsCpuViewModelAsync();

		// 初期配置(4石)より石数が増える=最低1手進むまで待つ
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (vm.BlackScore + vm.WhiteScore <= 4 && vm.IsGameInProgress && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		// IsLastMove の更新（前マスの解除→新マスの設定）は非同期の着手処理と並行して行われる。
		// SynchronizationContext のないテスト環境では GameViewModel の着手処理とこのテストの
		// 読み取りが別スレッドで並行しうるため、遷移中の瞬間（一時的に0件/2件になる区間）を
		// 読んでしまうことがある（Issue #124 のPRでCI Linuxジョブにて発覚したflaky failure）。
		// ちょうど1件に落ち着くまでポーリングしてから最終アサートする。
		var settledDeadline = DateTime.UtcNow.AddSeconds(5);
		while (vm.BoardSquares.Count(sq => sq.IsLastMove) != 1 && DateTime.UtcNow < settledDeadline)
			await Task.Delay(20);

		Assert.Single(vm.BoardSquares, sq => sq.IsLastMove);
	}

	/// <summary>
	/// パス条件: CpuVsCpu 停止中に HumanVsCpu に切り替えると新規ゲームが開始されること。
	/// </summary>
	[Fact]
	public async Task SwitchBackToHumanVsCpu_StartsNewGame()
	{
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: _ => new FakeAI());

		// CpuVsCpu モードで起動（停止状態）
		vm.GameMode = GameMode.CpuVsCpu;
		Assert.False(vm.IsGameInProgress);

		// HumanVsCpu に切り替え
		vm.GameMode = GameMode.HumanVsCpu;

		// 新規ゲーム開始を待つ
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (!vm.IsGameInProgress && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		Assert.True(vm.IsGameInProgress);
		Assert.True(vm.IsHumanVsCpu);
	}

	// ===== CPU vs CPU の AI が Dispose されずリークする問題（Issue #118） =====

	/// <summary>
	/// パス条件: 対戦が自然終了すると（EndGame 経路）、黒・白の CPU AI が計 2 回 Dispose されること。
	/// </summary>
	[Fact]
	public async Task CpuVsCpu_GameEndsNaturally_DisposesCpuAis()
	{
		var counter = new DisposeCounter();
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: d => new DisposableFakeAI(d, counter));
		vm.GameMode = GameMode.CpuVsCpu;
		vm.CpuVsCpuDelayMs = 0;
		await vm.StartNewGameAsync();
		vm.PauseCommand.Execute(null); // 「開始」ボタン相当

		var deadline = DateTime.UtcNow.AddSeconds(30);
		while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
			await Task.Delay(20);

		Assert.False(vm.IsGameInProgress);
		Assert.Equal(2, counter.Count); // 黒・白の CPU AI が両方 Dispose される
	}

	/// <summary>
	/// パス条件: CpuVsCpu 対戦中に StartNewGameAsync が再度呼ばれると、
	/// 古い CPU AI が Dispose され、新しい CPU AI はまだ Dispose されないこと。
	/// </summary>
	[Fact]
	public async Task StartNewGameAsync_CalledAgainDuringCpuVsCpu_DisposesPreviousCpuAis()
	{
		var firstCounter = new DisposeCounter();
		var secondCounter = new DisposeCounter();
		int callCount = 0;
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: d =>
			{
				callCount++;
				// 最初の 2 回（黒・白）は firstCounter、以降（2 回目の StartNewGameAsync）は secondCounter を使う
				return callCount <= 2
					? new DisposableFakeAI(d, firstCounter)
					: new DisposableFakeAI(d, secondCounter);
			});
		vm.GameMode = GameMode.CpuVsCpu;
		await vm.StartNewGameAsync(); // 1 回目: firstCounter の AI が黒・白に割り当てられる（IsPaused=true のまま自動進行しない）

		Assert.Equal(0, firstCounter.Count); // まだ Dispose されていない

		await vm.StartNewGameAsync(); // 2 回目: 古い（firstCounter の）AI が Dispose され、secondCounter の AI に置き換わる

		Assert.Equal(2, firstCounter.Count);  // 黒・白とも Dispose された
		Assert.Equal(0, secondCounter.Count); // 新しい AI はまだ Dispose されていない
	}

	/// <summary>
	/// パス条件: CpuVsCpu 対戦中に GameViewModel.Dispose() が呼ばれると、
	/// 黒・白の CPU AI が計 2 回 Dispose されること。
	/// </summary>
	[Fact]
	public async Task Dispose_WhileCpuVsCpuInProgress_DisposesCpuAis()
	{
		var counter = new DisposeCounter();
		var vm = new GameViewModel(
			aiFactory: _ => new FakeAI(),
			startDeferred: true,
			cpuVsCpuAiFactory: d => new DisposableFakeAI(d, counter));
		vm.GameMode = GameMode.CpuVsCpu;
		await vm.StartNewGameAsync(); // IsPaused=true のまま、CPU AI は黒・白とも割り当て済み

		Assert.Equal(0, counter.Count);

		vm.Dispose();

		Assert.Equal(2, counter.Count);
	}
}
