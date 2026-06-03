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
}
