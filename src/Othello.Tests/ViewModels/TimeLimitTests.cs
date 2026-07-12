namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Settings;
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

	// ===== 時間切れ強制着手 =====

	/// <summary>
	/// 時間切れ時に有効手[0]が自動着手されることを確認する。
	/// パス条件: ForcePlayForTest() 呼び出し後にボードが進んでいること（石数が変化）。
	/// </summary>
	[Fact]
	public async Task TimeUp_ForcesMove()
	{
		var vm = MakeVm(DifficultyLevel.Medium);
		vm.IsTimeLimitEnabled = true;
		vm.TimeLimitSeconds = 1;
		vm.StartNewGame();

		// 1 秒待てば自動着手が走るはずだが、テストの速度のため直接強制着手を呼ぶ
		await vm.ForcePlayForTestAsync();

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

		// AI の応答を待つ
		await Task.Delay(800);

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

		await Task.Delay(1500); // 1 秒ティックを跨いで待つ

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
}
