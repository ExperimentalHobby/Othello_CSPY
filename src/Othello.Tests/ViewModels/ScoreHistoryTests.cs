namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Settings;
using Technopro.Othello.ViewModels;

/// <summary>
/// GameViewModel の ScoreHistory 動作テスト。
/// 初期エントリ・着手後の追加・Undo 後の削除を検証する。
/// </summary>
public class ScoreHistoryTests
{
    private sealed class FakeAI : IAIStrategy
    {
        public DifficultyLevel Difficulty => DifficultyLevel.Medium;
        public string EngineName => "AI: Fake";
        public Position GetBestMove(Board board, PlayerColor playerColor)
            => OthelloRules.GetValidMoves(board, playerColor)[0];
    }

    private static GameViewModel MakeVm()
        => new(d => new FakeAI(), settings: new OthelloSettings());

    /// <summary>
    /// 新規ゲーム開始直後に ScoreHistory に 1 件の初期エントリが入ることを確認する。
    /// パス条件: ScoreHistory.Count == 1。
    /// </summary>
    [Fact]
    public void NewGame_ScoreHistory_HasOneInitialEntry()
    {
        var vm = MakeVm();
        Assert.Single(vm.ScoreHistory);
    }

    /// <summary>
    /// ScoreHistory の初期エントリが Black=2, White=2 であることを確認する。
    /// パス条件: BlackCount == 2 && WhiteCount == 2。
    /// </summary>
    [Fact]
    public void NewGame_InitialEntry_IsBlack2White2()
    {
        var vm = MakeVm();
        var entry = vm.ScoreHistory[0];
        Assert.Equal(2, entry.BlackCount);
        Assert.Equal(2, entry.WhiteCount);
    }

    /// <summary>
    /// 人間が 1 手着手後に ScoreHistory が 2 件になることを確認する。
    /// パス条件: ScoreHistory.Count == 2。
    /// </summary>
    [Fact]
    public async Task AfterHumanMove_ScoreHistory_HasTwoEntries()
    {
        var vm = MakeVm();
        // 黒（人間）が先手
        var validMoves = OthelloRules.GetValidMoves(vm.EngineCurrentBoard, PlayerColor.Black);
        vm.SquareClickedCommand.Execute(validMoves[0]);

        // AI 応答を待つ
        await Task.Delay(200);

        // 人間 1 手 + AI 1 手 = 3 件（初期 + 2）または人間のみ = 2 件
        Assert.True(vm.ScoreHistory.Count >= 2);
    }

    /// <summary>
    /// 黒が着手後、最新エントリの BlackCount が初期値より増えることを確認する。
    /// パス条件: ScoreHistory.Last().BlackCount > 2。
    /// </summary>
    [Fact]
    public async Task AfterBlackMove_LatestEntry_BlackCountIncreases()
    {
        var vm = MakeVm();
        var validMoves = OthelloRules.GetValidMoves(vm.EngineCurrentBoard, PlayerColor.Black);
        vm.SquareClickedCommand.Execute(validMoves[0]);

        await Task.Delay(200);

        Assert.True(vm.ScoreHistory[1].BlackCount > 2);
    }

    /// <summary>
    /// Undo 後に ScoreHistory が 1 件（初期状態）に戻ることを確認する。
    /// パス条件: ScoreHistory.Count == 1。
    /// </summary>
    [Fact]
    public async Task AfterUndo_ScoreHistory_ResetsToInitial()
    {
        var vm = MakeVm();
        var validMoves = OthelloRules.GetValidMoves(vm.EngineCurrentBoard, PlayerColor.Black);
        vm.SquareClickedCommand.Execute(validMoves[0]);

        // AI 応答を待ってから Undo
        await Task.Delay(500);

        vm.UndoCommand.Execute(null);

        Assert.Single(vm.ScoreHistory);
    }
}
