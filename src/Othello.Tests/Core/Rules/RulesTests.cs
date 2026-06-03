namespace Technopro.Othello.Tests.Core.Rules;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

public class OthelloRulesTests
{
    [Fact]
    public void GetValidMoves_BlackStartPosition_Returns4Moves()
    {
        var board = new Board();
        var moves = OthelloRules.GetValidMoves(board, PlayerColor.Black);
        Assert.Equal(4, moves.Count);
        Assert.Contains(new Position(3, 2), moves);
        Assert.Contains(new Position(2, 3), moves);
        Assert.Contains(new Position(4, 5), moves);
        Assert.Contains(new Position(5, 4), moves);
    }

    [Fact]
    public void IsValidMove_ValidPosition_ReturnsTrue()
    {
        var board = new Board();
        Assert.True(OthelloRules.IsValidMove(board, new Position(3, 2), PlayerColor.Black));
    }

    [Fact]
    public void IsValidMove_InvalidPosition_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.IsValidMove(board, new Position(0, 0), PlayerColor.Black));
    }

    [Fact]
    public void MakeMove_ValidMove_UpdatesBoard()
    {
        var board = new Board();
        int beforeCount = board.CountPieces(PlayerColor.Black);
        OthelloRules.MakeMove(board, new Position(3, 2), PlayerColor.Black);
        Assert.True(board.CountPieces(PlayerColor.Black) > beforeCount);
    }

    [Fact]
    public void CanPass_InitialBoard_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.CanPass(board, PlayerColor.Black));
    }

    [Fact]
    public void IsGameOver_InitialBoard_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.IsGameOver(board));
    }
}

public class FlipCalculatorTests
{
    [Fact]
    public void GetFlippablePieces_ValidPosition_ReturnsFlippedPieces()
    {
        var board = new Board();
        var flipped = FlipCalculator.GetFlippablePieces(board, new Position(3, 2), PlayerColor.Black);
        Assert.Single(flipped);
        Assert.Contains(new Position(3, 3), flipped);
    }

    [Fact]
    public void GetFlippablePieces_NoFlips_ReturnsEmpty()
    {
        var board = new Board();
        var flipped = FlipCalculator.GetFlippablePieces(board, new Position(0, 0), PlayerColor.Black);
        Assert.Empty(flipped);
    }
}
