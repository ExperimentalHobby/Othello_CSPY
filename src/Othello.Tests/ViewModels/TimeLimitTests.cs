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
        => new(d => new FakeAI(d), settings: new OthelloSettings()) { DifficultyIndex = (int)diff - 1 };

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
    /// パス条件: DifficultyIndex = 2 後 IsTimeLimitEnabled == true。
    /// </summary>
    [Fact]
    public void SetDifficulty_Hard_EnablesTimeLimit()
    {
        var vm = MakeVm(DifficultyLevel.Medium);
        vm.DifficultyIndex = 2; // Hard
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
        vm.DifficultyIndex = 0; // Easy
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
        vm.TimeLimitSeconds   = 15;
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
        vm.TimeLimitSeconds   = 1;
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
        vm.TimeLimitSeconds   = 20;
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
}
