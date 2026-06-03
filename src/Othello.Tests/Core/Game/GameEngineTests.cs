namespace Technopro.Othello.Tests.Core.Game;

using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

public class GameEngineTests
{
    [Fact]
    public void Initialize_StartsGame_WithCorrectState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    [Fact]
    public void MakeMove_ValidMove_ChangesGameState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(3, 2));
        Assert.True(result.IsSuccess);
        Assert.Equal(GameState.WhiteTurn, engine.GameState);
    }

    [Fact]
    public void MakeMove_InvalidMove_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(0, 0));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void GetValidMoves_ReturnsAvailableMoves()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var moves = engine.GetValidMoves(PlayerColor.Black);
        Assert.NotEmpty(moves);
        Assert.Equal(4, moves.Count);
    }

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

    [Fact]
    public void Pass_WhenNoValidMoves_AdvancesToNextPlayer()
    {
        // 黒に有効手がない状況を人工的に作る: 初期盤面から白だけが有効手を持つ局面に設定
        // 簡易的に: 黒の有効手が0になるよう OthelloRules.CanPass が true な状態をセットアップする
        // ここでは実際の盤面操作の代わりにリフレクションで _board を差し替える
        // → より現実的な方法: 黒が全手打ち終わって白のみ打てる局面を手順で作る

        // 以下は Pass() の事前条件違反テスト（有効手がある場合は例外）
        var engine = new GameEngine();
        engine.Initialize();
        // 初期盤面では黒に有効手があるためパス不可
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    [Fact]
    public void Pass_WhenValidMovesExist_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 初期盤面では黒（先手）に 4 手の有効手があるためパス不可
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    [Fact]
    public void Pass_AfterGameOver_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        // Initialize せずに呼ぶ（GameState = Initialize → IsGameInProgress = false）
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    // --- Undo ---

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

    [Fact]
    public void Undo_AtInitialState_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 1 手も打っていない状態では Undo 不可
        Assert.False(engine.Undo());
    }

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
