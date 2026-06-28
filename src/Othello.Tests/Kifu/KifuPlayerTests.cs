namespace Technopro.Othello.Tests.Kifu;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// KifuPlayer の単体テスト。
/// StepForward / StepBack / GoToStart / GoToEnd の挙動と境界条件を検証する。
/// </summary>
public class KifuPlayerTests
{
    /// <summary>
    /// 黒が (2,3) に着手する 1 手だけの棋譜と KifuPlayer を返す。
    /// 初期盤面から黒 (2,3) が最も基本的な合法手のため再現しやすい。
    /// </summary>
    private static (KifuRecord record, KifuPlayer player) MakeSingleMoveKifu()
    {
        var moves = new List<KifuMove>
        {
            new(PlayerColor.Black, Row: 2, Col: 3),
        };
        var record = new KifuRecord(1, DateTimeOffset.Now,
            PlayerColor.Black, DifficultyLevel.Easy,
            Result: null, moves, new KifuFinalScore(4, 1));
        return (record, new KifuPlayer(record));
    }

    /// <summary>
    /// 初期盤面が標準オセロ配置（黒 2・白 2）であることを確認する。
    /// パス条件: CountPieces(Black) == 2、CountPieces(White) == 2。
    /// </summary>
    [Fact]
    public void InitialBoard_IsStandardOthelloSetup()
    {
        var (_, player) = MakeSingleMoveKifu();
        var board = player.CurrentBoard;
        Assert.Equal(2, board.CountPieces(PlayerColor.Black));
        Assert.Equal(2, board.CountPieces(PlayerColor.White));
    }

    /// <summary>
    /// StepForward で着手が盤面に反映されることを確認する。
    /// パス条件: StepForward 後に (2,3) が Black であること。
    /// </summary>
    [Fact]
    public void StepForward_AppliesMoveToBoard()
    {
        var (_, player) = MakeSingleMoveKifu();
        player.StepForward();
        Assert.Equal(PlayerColor.Black, player.CurrentBoard.GetPiece(2, 3));
    }

    /// <summary>
    /// StepForward 後 StepBack すると初期盤面に戻ることを確認する。
    /// パス条件: StepBack 後に (2,3) が Empty であること。
    /// </summary>
    [Fact]
    public void StepBack_AfterForward_RestoresBoard()
    {
        var (_, player) = MakeSingleMoveKifu();
        player.StepForward();
        player.StepBack();
        Assert.Equal(PlayerColor.Empty, player.CurrentBoard.GetPiece(2, 3));
        Assert.Equal(0, player.CurrentMoveIndex);
    }

    /// <summary>
    /// 最後の手を超えて StepForward を呼んでも例外が発生せず no-op になることを確認する。
    /// パス条件: CanStepForward = false の状態で StepForward しても CurrentMoveIndex が変わらないこと。
    /// </summary>
    [Fact]
    public void StepForward_AtEnd_IsNoOp()
    {
        var (_, player) = MakeSingleMoveKifu();
        player.GoToEnd();
        Assert.False(player.CanStepForward);
        var before = player.CurrentMoveIndex;
        player.StepForward(); // no-op のはず
        Assert.Equal(before, player.CurrentMoveIndex);
    }

    /// <summary>
    /// 先頭で StepBack を呼んでも例外が発生せず no-op になることを確認する。
    /// パス条件: CanStepBack = false の状態で StepBack しても CurrentMoveIndex が 0 のままであること。
    /// </summary>
    [Fact]
    public void StepBack_AtStart_IsNoOp()
    {
        var (_, player) = MakeSingleMoveKifu();
        Assert.False(player.CanStepBack);
        player.StepBack(); // no-op のはず
        Assert.Equal(0, player.CurrentMoveIndex);
    }

    /// <summary>
    /// GoToEnd 後の盤面の石数が KifuRecord.FinalScore と一致することを確認する。
    /// パス条件: black + white == 全 64 マスのうち着手分の占有数であること（1 手なので 5 石）。
    /// </summary>
    [Fact]
    public void GoToEnd_BoardMatchesFinalPosition()
    {
        var (_, player) = MakeSingleMoveKifu();
        player.GoToEnd();
        // 初期 4 石 + 黒 (2,3) 着手 + 白 (3,3) 反転 → 黒 4, 白 1
        Assert.Equal(4, player.CurrentBoard.CountPieces(PlayerColor.Black));
        Assert.Equal(1, player.CurrentBoard.CountPieces(PlayerColor.White));
    }

    /// <summary>
    /// GoToStart で CurrentMoveIndex が 0 に戻ることを確認する。
    /// パス条件: GoToEnd 後に GoToStart を呼ぶと CurrentMoveIndex == 0。
    /// </summary>
    [Fact]
    public void GoToStart_ResetsToBeginning()
    {
        var (_, player) = MakeSingleMoveKifu();
        player.GoToEnd();
        player.GoToStart();
        Assert.Equal(0, player.CurrentMoveIndex);
        Assert.False(player.CanStepBack);
    }

    /// <summary>
    /// パスを含む棋譜を StepForward しても例外なく進めることを確認する。
    /// パス条件: パス手を含む全手を StepForward で消化できること。
    /// </summary>
    [Fact]
    public void StepForward_WithPassMove_DoesNotThrow()
    {
        var moves = new List<KifuMove>
        {
            new(PlayerColor.Black, Row: 2, Col: 3),
            new(PlayerColor.White, IsPass: true),
        };
        var record = new KifuRecord(1, DateTimeOffset.Now,
            PlayerColor.Black, DifficultyLevel.Easy,
            null, moves, new KifuFinalScore(0, 0));
        var player = new KifuPlayer(record);

        var ex = Record.Exception(() =>
        {
            player.StepForward();
            player.StepForward();
        });
        Assert.Null(ex);
        Assert.Equal(2, player.CurrentMoveIndex);
    }

    /// <summary>
    /// TotalMoves が棋譜の手数と一致することを確認する。
    /// パス条件: TotalMoves == Moves.Count。
    /// </summary>
    [Fact]
    public void TotalMoves_MatchesKifuMovesCount()
    {
        var (record, player) = MakeSingleMoveKifu();
        Assert.Equal(record.Moves.Count, player.TotalMoves);
    }

    /// <summary>
    /// CurrentMoveIndex は初期値 0 であることを確認する。
    /// パス条件: 生成直後の CurrentMoveIndex == 0。
    /// </summary>
    [Fact]
    public void CurrentMoveIndex_InitiallyZero()
    {
        var (_, player) = MakeSingleMoveKifu();
        Assert.Equal(0, player.CurrentMoveIndex);
    }
}
