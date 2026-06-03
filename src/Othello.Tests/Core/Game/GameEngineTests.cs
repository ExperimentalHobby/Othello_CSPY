namespace Technopro.Othello.Tests.Core.Game;

using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

public class GameEngineTests
{
    /// <summary>
    /// Initialize() 後のゲーム状態が正しいことを確認する。
    /// パス条件: GameState が BlackTurn、黒・白ともに石数が 2 であること。
    /// </summary>
    [Fact]
    public void Initialize_StartsGame_WithCorrectState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    /// <summary>
    /// 有効な手を打つと着手成功かつターンが相手に移ることを確認する。
    /// パス条件: MakeMove の戻り値が IsSuccess = true、かつ GameState が WhiteTurn に変わること。
    /// </summary>
    [Fact]
    public void MakeMove_ValidMove_ChangesGameState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(3, 2));
        Assert.True(result.IsSuccess);
        Assert.Equal(GameState.WhiteTurn, engine.GameState);
    }

    /// <summary>
    /// 石を置けない位置（隅など）への着手は失敗を返すことを確認する。
    /// パス条件: MakeMove の戻り値が IsSuccess = false であること。
    /// </summary>
    [Fact]
    public void MakeMove_InvalidMove_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(0, 0));
        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// 初期盤面における黒の有効手が 4 件であることを確認する。
    /// パス条件: GetValidMoves の結果が空でなく、件数が 4 であること。
    /// </summary>
    [Fact]
    public void GetValidMoves_ReturnsAvailableMoves()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var moves = engine.GetValidMoves(PlayerColor.Black);
        Assert.NotEmpty(moves);
        Assert.Equal(4, moves.Count);
    }

    /// <summary>
    /// 初期盤面では勝者が決まっていないことを確認する。
    /// パス条件: winner が null、黒・白ともに石数が 2 であること。
    /// </summary>
    [Fact]
    public void GetResult_InitialBoard_ReturnsNullWinner()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var (winner, blackScore, whiteScore) = engine.GetResult();
        Assert.Null(winner);
        Assert.Equal(2, blackScore);
        Assert.Equal(2, whiteScore);
    }

    // --- Pass ---

    /// <summary>
    /// 有効手がある状態で Pass() を呼ぶと例外が投げられることを確認する。
    /// パス条件: InvalidOperationException がスローされること。
    /// </summary>
    [Fact]
    public void Pass_WhenValidMovesExist_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 初期盤面では黒（先手）に 4 手の有効手があるためパス不可
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    /// <summary>
    /// ゲームが開始されていない状態（Initialize 前）で Pass() を呼ぶと例外が投げられることを確認する。
    /// パス条件: InvalidOperationException がスローされること。
    /// </summary>
    [Fact]
    public void Pass_AfterGameOver_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        // Initialize せずに呼ぶ（GameState = Initialize → IsGameInProgress = false）
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    // --- Undo ---

    /// <summary>
    /// 1 手打った後に Undo() すると、盤面・スコア・ターンが着手前の状態に戻ることを確認する。
    /// パス条件: Undo の戻り値が true、GameState が BlackTurn、黒・白ともに石数が 2 に戻ること。
    /// </summary>
    [Fact]
    public void Undo_AfterOneMove_RestoresPreviousState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 黒が (2,3) に打つ（初期盤面の有効手のひとつ）
        engine.MakeMove(new Position(2, 3));
        Assert.Equal(GameState.WhiteTurn, engine.GameState);

        var result = engine.Undo();

        Assert.True(result);
        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    /// <summary>
    /// 1 手も打っていない初期状態では Undo() が失敗することを確認する。
    /// パス条件: Undo の戻り値が false であること。
    /// </summary>
    [Fact]
    public void Undo_AtInitialState_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 1 手も打っていない状態では Undo 不可
        Assert.False(engine.Undo());
    }

    /// <summary>
    /// 2 手打った後に Undo() を 2 回繰り返すと初期状態に戻ることを確認する。
    /// パス条件: GameState が BlackTurn、黒・白ともに石数が 2 に戻ること。
    /// </summary>
    [Fact]
    public void Undo_MultipleTimes_RestoresEarlierState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        engine.MakeMove(new Position(2, 3)); // 黒
        engine.MakeMove(new Position(2, 2)); // 白
        engine.Undo(); // 白を戻す
        engine.Undo(); // 黒を戻す

        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }
}
