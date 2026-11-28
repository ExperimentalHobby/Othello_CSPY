namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Settings;
using Technopro.Othello.Tests.Helpers;
using Technopro.Othello.ViewModels;

/// <summary>
/// GameViewModel の制限時間モード テスト。
/// IsTimeLimitEnabled の難易度連動・タイマー起動・強制着手を検証する。
/// </summary>
public class TimeLimitTests
{
	private sealed class FakeAI : IAIStrategy
	{
		public DifficultyLevel Difficulty { get; }
		public string EngineName => "AI: Fake";
		public FakeAI(DifficultyLevel d) => Difficulty = d;
		public Position GetBestMove(Board board, PlayerColor playerColor)
			=> OthelloRules.GetValidMoves(board, playerColor)[0];
	}

	private static GameViewModel MakeVm(DifficultyLevel diff = DifficultyLevel.Medium)
		=> new(d => new FakeAI(d), settings: new OthelloSettings()) { DifficultyIndex = (int)diff };

	/// <summary>gate が閉じている間 AI 生成が完了しない ViewModel を作る（準備中の状態を再現する）。</summary>
	private static GameViewModel MakeGatedVm(ManualResetEventSlim gate)
		=> new(d =>
		{
			gate.Wait(TimeSpan.FromSeconds(10));
			return new FakeAI(d);
		}, startDeferred: true, settings: new OthelloSettings());

	// ===== IsTimeLimitEnabled の難易度連動 =====

	/// <summary>
	/// 初期状態（Normal）で IsTimeLimitEnabled が false であることを確認する。
	/// パス条件: IsTimeLimitEnabled == false。
	/// </summary>
	[Fact]
	public void InitialState_Normal_IsTimeLimitEnabled_IsFalse()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		Assert.False(vm.IsTimeLimitEnabled);
	}

	/// <summary>
	/// 難易度を Hard に変更すると IsTimeLimitEnabled が true になることを確認する。
	/// パス条件: DifficultyIndex = 3 後 IsTimeLimitEnabled == true。
	/// </summary>
	[Fact]
	public void SetDifficulty_Hard_EnablesTimeLimit()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.DifficultyIndex = 3; // Hard（Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4）
		Assert.True(vm.IsTimeLimitEnabled);
	}

	/// <summary>
	/// 難易度を Expert に変更すると IsTimeLimitEnabled が true になることを確認する（Issue #76 回帰）。
	/// パス条件: DifficultyIndex = 4 後 IsTimeLimitEnabled == true。
	/// </summary>
	[Fact]
	public void SetDifficulty_Expert_EnablesTimeLimit()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.DifficultyIndex = 4; // Expert（Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4）
		Assert.True(vm.IsTimeLimitEnabled);
	}

	/// <summary>
	/// 難易度を Easy に変更すると IsTimeLimitEnabled が false になることを確認する。
	/// パス条件: Hard → Easy 変更後 IsTimeLimitEnabled == false。
	/// </summary>
	[Fact]
	public void SetDifficulty_Easy_DisablesTimeLimit()
	{
		var vm = MakeVm(DifficultyLevel.Hard);
		vm.DifficultyIndex = 1; // Easy（Beginner=0, Easy=1）
		Assert.False(vm.IsTimeLimitEnabled);
	}

	/// <summary>
	/// ユーザーが手動で IsTimeLimitEnabled を切り替えられることを確認する。
	/// パス条件: Normal でも true にセットできること。
	/// </summary>
	[Fact]
	public void ManualToggle_OverridesDifficultyDefault()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		Assert.False(vm.IsTimeLimitEnabled);
		vm.IsTimeLimitEnabled = true;
		Assert.True(vm.IsTimeLimitEnabled);
	}

	// ===== TimeLimitSeconds =====

	/// <summary>
	/// デフォルトの TimeLimitSeconds が 30 であることを確認する。
	/// パス条件: TimeLimitSeconds == 30。
	/// </summary>
	[Fact]
	public void TimeLimitSeconds_Default_Is30()
	{
		var vm = MakeVm();
		Assert.Equal(30, vm.TimeLimitSeconds);
	}

	// ===== RemainingSeconds =====

	/// <summary>
	/// 制限時間 OFF のとき人間ターン開始で RemainingSeconds が 0 のままであることを確認する。
	/// パス条件: RemainingSeconds == 0。
	/// </summary>
	[Fact]
	public void TimerOff_HumanTurn_RemainingSecondsIsZero()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = false;
		vm.StartNewGame();
		// AI は白 → 人間（黒）が先手
		Assert.Equal(0, vm.RemainingSeconds);
	}

	/// <summary>
	/// 制限時間 ON のとき人間ターン開始で RemainingSeconds が TimeLimitSeconds と等しいことを確認する。
	/// パス条件: RemainingSeconds == TimeLimitSeconds。
	/// </summary>
	[Fact]
	public void TimerOn_HumanTurn_RemainingSeconds_EqualsTimeLimitSeconds()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 15;
		vm.StartNewGame();
		// 黒が先手（人間）のとき StartNewGame() 直後に RemainingSeconds がセットされる
		Assert.Equal(15, vm.RemainingSeconds);
	}

	/// <summary>
	/// アプリ起動後の最初のゲーム開始（コンストラクタからの暗黙の StartNewGame() を発生させず、
	/// IsGameInProgress が false のままの状態からの最初の StartNewGame() 呼び出し）でも
	/// タイマーが起動することを確認する（Issue #152 回帰）。
	/// パス条件: RemainingSeconds == TimeLimitSeconds。
	/// </summary>
	[Fact]
	public void TimerOn_FirstGameEver_HumanTurn_RemainingSeconds_EqualsTimeLimitSeconds()
	{
		var vm = new GameViewModel(d => new FakeAI(d), startDeferred: true, settings: new OthelloSettings());
		Assert.False(vm.IsGameInProgress);

		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 15;
		vm.StartNewGame();

		// 黒が先手（人間）のとき StartNewGame() 直後に RemainingSeconds がセットされる
		Assert.Equal(15, vm.RemainingSeconds);
	}

	// ===== #260: 設定変更の即時反映 =====

	/// <summary>
	/// 制限時間 ON でタイマー起動中に OFF にすると、タイマーが停止することを確認する。
	/// パス条件: RemainingSeconds == 0 かつ IsTimerRunning == false。
	/// </summary>
	[Fact]
	public void DisableTimeLimit_BeforeFirstMove_StopsTimer()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();
		Assert.True(vm.IsTimerRunning); // 前提条件確認

		vm.IsTimeLimitEnabled = false;

		Assert.Equal(0, vm.RemainingSeconds);
		Assert.False(vm.IsTimerRunning);
	}

	/// <summary>
	/// 初手前に制限時間を OFF から ON にすると、その場でカウントダウンが始まることを確認する。
	/// パス条件: RemainingSeconds == TimeLimitSeconds。
	/// </summary>
	[Fact]
	public void EnableTimeLimit_BeforeFirstMove_StartsTimer()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.TimeLimitSeconds = 15;
		vm.StartNewGame();
		Assert.False(vm.IsTimerRunning); // 前提条件確認

		vm.IsTimeLimitEnabled = true;

		Assert.Equal(15, vm.RemainingSeconds);
	}

	/// <summary>
	/// 初手前に秒数を変更すると、起動中のタイマーが新しい秒数で数え直されることを確認する。
	/// パス条件: RemainingSeconds == 変更後の TimeLimitSeconds。
	/// </summary>
	[Fact]
	public void ChangeTimeLimitSeconds_BeforeFirstMove_RestartsTimerWithNewValue()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();
		Assert.Equal(30, vm.RemainingSeconds); // 前提条件確認

		vm.TimeLimitSeconds = 45;

		Assert.Equal(45, vm.RemainingSeconds);
	}

	/// <summary>
	/// カウントダウン中に同じ秒数を再設定しても、残り時間が巻き戻らないことを確認する。
	/// パス条件: 再設定の前後で RemainingSeconds が TimeLimitSeconds 未満のままであること。
	/// </summary>
	[Fact]
	public async Task SetSameTimeLimitSeconds_DoesNotResetCountdown()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();
		await TestWait.UntilAsync(() => vm.RemainingSeconds < 30, "カウントダウンが進みませんでした");

		vm.TimeLimitSeconds = 30;

		Assert.True(vm.RemainingSeconds < 30);
	}

	/// <summary>
	/// 制限時間が OFF のとき時間切れ処理が呼ばれても、自動着手されないことを確認する。
	/// パス条件: 石の合計が初期状態の 4 のままであること。
	/// </summary>
	[Fact]
	public void TurnTimerExpired_WhenTimeLimitDisabled_DoesNotPlay()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.StartNewGame();
		Assert.False(vm.IsTimeLimitEnabled); // 前提条件確認

		vm.ExpireTurnTimerForTest();

		Assert.Equal(4, vm.BlackScore + vm.WhiteScore);
	}

	/// <summary>
	/// 起動中のタイマーを OFF にした後は、元の制限時間が過ぎても自動着手されないことを確認する（結合）。
	/// パス条件: 1.5 秒後も石の合計が 4 のままであること。
	/// </summary>
	[Fact]
	public async Task DisableTimeLimit_AfterOriginalLimitElapses_DoesNotPlay()
	{
		using var vm = MakeVm(DifficultyLevel.Medium);
		vm.TimeLimitSeconds = 1;
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();

		vm.IsTimeLimitEnabled = false;

		// 「着手されないこと」の否定系検証のため、制限時間を超える固定時間を待つ
		await Task.Delay(1500);

		Assert.Equal(4, vm.BlackScore + vm.WhiteScore);
	}

	// ===== #260: 新規ゲーム準備中のタイマー =====

	/// <summary>
	/// 新規ゲームの AI 生成を待っている間、前のゲームのタイマーが停止していることを確認する。
	/// パス条件: 準備中は RemainingSeconds == 0。
	/// </summary>
	[Fact]
	public async Task StartNewGameAsync_WhilePreparingAI_StopsTimer()
	{
		using var gate = new ManualResetEventSlim(initialState: true);
		using var vm = MakeGatedVm(gate);
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();
		Assert.True(vm.IsTimerRunning); // 前提条件確認

		gate.Reset();
		var starting = vm.StartNewGameAsync();
		int remainingWhilePreparing = vm.RemainingSeconds;
		gate.Set();
		await starting;

		Assert.Equal(0, remainingWhilePreparing);
	}

	/// <summary>
	/// 新規ゲームの準備中に制限時間を切り替えてもタイマーは起動せず、準備完了後に起動することを確認する。
	/// パス条件: 準備中は RemainingSeconds == 0、完了後は TimeLimitSeconds と等しいこと。
	/// </summary>
	[Fact]
	public async Task ToggleTimeLimit_WhilePreparingAI_StartsTimerOnlyAfterReady()
	{
		using var gate = new ManualResetEventSlim(initialState: true);
		using var vm = MakeGatedVm(gate);
		vm.IsTimeLimitEnabled = true;
		vm.StartNewGame();

		gate.Reset();
		var starting = vm.StartNewGameAsync();
		vm.IsTimeLimitEnabled = false;
		vm.IsTimeLimitEnabled = true;
		int remainingWhilePreparing = vm.RemainingSeconds;
		gate.Set();
		await starting;

		Assert.Equal(0, remainingWhilePreparing);
		Assert.Equal(30, vm.RemainingSeconds);
	}

	// ===== 時間切れ強制着手 =====

	/// <summary>
	/// 時間切れ時に有効手[0]が自動着手されることを確認する。
	/// パス条件: ForcePlayForTest() 呼び出し後にボードが進んでいること（石数が変化）。
	/// </summary>
	[Fact]
	public async Task TimeUp_ForcesMove()
	{
		// 強制着手を直接呼ぶため、制限時間タイマーは有効化しない。
		// 有効化すると AI 応答後に 1 秒タイマーが再び自動着手し、石数が 6 を超えて待機条件が成立しなくなり得る（Issue #222）
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.StartNewGame();

		// 時間切れ時に呼ばれる強制着手を、タイマーを待たず直接呼ぶ
		vm.ForcePlayFirstValidMove();
		// 着手後にAIが非同期(fire-and-forget)で応答するため、応答の反映（石数 +2、処理中フラグ解除）を待つ
		await TestWait.UntilAsync(
			() => vm.BlackScore + vm.WhiteScore == 6 && !vm.IsAIThinking,
			"強制着手後の AI 応答が反映されませんでした");

		// 人間（黒）が着手したので黒石が増えているはず
		Assert.True(vm.BlackScore > 2);
	}

	// ===== Undo =====

	/// <summary>
	/// Undo でタイマーがリセットされ、制限時間 ON なら RemainingSeconds が
	/// TimeLimitSeconds に戻ることを確認する。
	/// パス条件: 着手 → Undo → RemainingSeconds == TimeLimitSeconds。
	/// </summary>
	[Fact]
	public async Task Undo_ResetsTimer()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 20;
		vm.StartNewGame();

		// 人間が 1 手打つ（AI 先手ではないので黒が先手）
		var validMoves = OthelloRules.GetValidMoves(vm.EngineCurrentBoard, PlayerColor.Black);
		vm.SquareClickedCommand.Execute(validMoves[0]);

		// AI の応答（石数 +2、処理中フラグ解除）を待つ。Undo は AI 思考中は受け付けられないため完了まで待つ必要がある
		await TestWait.UntilAsync(
			() => vm.BlackScore + vm.WhiteScore == 6 && !vm.IsAIThinking,
			"AI の応答が反映されませんでした");

		vm.UndoCommand.Execute(null);

		// Undo 後、人間ターンに戻るのでタイマーがリセットされる
		Assert.Equal(20, vm.RemainingSeconds);
	}

	// ===== #57: 終局・Dispose 時のタイマー停止 =====

	/// <summary>
	/// 終局時（両者に有効手がない盤面）にタイマーが確実に停止することを確認する。
	/// LoadStateForTest() の RefreshBoardDisplay()（タイマー起動）→ 終局判定 → EndGame() という
	/// 実際のバグ経路（人間の最終手直後は CurrentPlayer が人間のままタイマーが再起動してしまう）を再現する。
	/// パス条件: EndGame 後に IsGameInProgress=false、RemainingSeconds=0、IsTimerRunning=false であること。
	/// </summary>
	[Fact]
	public void EndGame_WithTimeLimitEnabled_StopsTimer()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 30;

		// 全マスを黒で埋め、両者に有効手がない終局盤面を用意する
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Black);

		vm.LoadStateForTest(board, PlayerColor.Black);

		Assert.False(vm.IsGameInProgress);
		Assert.Equal(0, vm.RemainingSeconds);
		Assert.False(vm.IsTimerRunning);
	}

	/// <summary>
	/// Dispose() 後にタイマーのバックグラウンドタスク（RunTurnTimerAsync）が停止し、
	/// RemainingSeconds が変化し続けないことを確認する。
	/// パス条件: Dispose 後 1.5 秒待っても RemainingSeconds が変化しないこと。
	/// </summary>
	[Fact]
	public async Task Dispose_StopsTurnTimerBackgroundTask()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 30;
		vm.StartNewGame(); // 黒（人間）が先手 → タイマー起動

		Assert.True(vm.IsTimerRunning); // 前提条件確認

		int remainingAtDispose = vm.RemainingSeconds;
		vm.Dispose();

		// 「Dispose 後にタイマーが動き続けないこと」の否定系検証のため、観測できる完了シグナルが無く
		// 1 秒ティックを跨ぐ固定時間を待つ（Issue #222）
		await Task.Delay(1500);

		Assert.Equal(remainingAtDispose, vm.RemainingSeconds);
	}

	// ===== SaveTimeLimitSettings の永続化結合テスト =====

	/// <summary>
	/// SaveTimeLimitSettings() で保存した TimeLimitSeconds が、
	/// 同じ設定ファイルパスを指す別の GameViewModel インスタンス（settings 省略 = Load() 経由）
	/// に正しく反映されることを確認する。
	/// パス条件: 新しいインスタンスの TimeLimitSeconds が保存した値と一致すること。
	/// </summary>
	[Fact]
	public void SaveTimeLimitSettings_ReflectsInNewInstance_ViaFile()
	{
		var tmpFile = Path.Combine(Path.GetTempPath(), $"othello_settings_test_{Guid.NewGuid():N}.json");
		try
		{
			var vm1 = new GameViewModel(d => new FakeAI(d), settings: new OthelloSettings(), settingsFilePath: tmpFile);
			vm1.TimeLimitSeconds = 42;
			vm1.SaveTimeLimitSettings();

			// settings を省略することで OthelloSettingsManager.Load(tmpFile) 経由の読み込みを強制する
			var vm2 = new GameViewModel(d => new FakeAI(d), settingsFilePath: tmpFile);

			Assert.Equal(42, vm2.TimeLimitSeconds);
		}
		finally
		{
			if (File.Exists(tmpFile))
				File.Delete(tmpFile);
		}
	}

	/// <summary>
	/// 書き込み不可能な保存先（既存ディレクトリをファイルパスとして注入）でも
	/// SaveTimeLimitSettings() が例外を投げないことを確認する。
	/// パス条件: 例外が伝播しないこと。
	/// </summary>
	[Fact]
	public void SaveTimeLimitSettings_WhenTargetIsDirectory_DoesNotThrow()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"othello_settings_dir_{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		try
		{
			var vm = new GameViewModel(d => new FakeAI(d), settings: new OthelloSettings(), settingsFilePath: directoryPath);

			var exception = Record.Exception(() => vm.SaveTimeLimitSettings());

			Assert.Null(exception);
		}
		finally
		{
			Directory.Delete(directoryPath);
		}
	}
}
