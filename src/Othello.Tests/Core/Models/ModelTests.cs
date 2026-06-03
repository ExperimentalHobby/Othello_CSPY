namespace Technopro.Othello.Tests.Core.Models;

using Technopro.Othello.Core.Models;

public class BoardTests
{
    [Fact]
    public void Constructor_CreatesBoard_WithInitialPieces()
    {
        var board = new Board();
        Assert.Equal(2, board.CountPieces(PlayerColor.Black));
        Assert.Equal(2, board.CountPieces(PlayerColor.White));
    }

    [Fact]
    public void GetPiece_ReturnsCorrectColor()
    {
        var board = new Board();
        Assert.Equal(PlayerColor.White, board.GetPiece(3, 3));
        Assert.Equal(PlayerColor.Black, board.GetPiece(3, 4));
    }

    [Fact]
    public void SetPiece_UpdatesPosition()
    {
        var board = new Board();
        board.SetPiece(0, 0, PlayerColor.Black);
        Assert.Equal(PlayerColor.Black, board.GetPiece(0, 0));
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var board1 = new Board();
        var board2 = board1.Clone();
        board2.SetPiece(0, 0, PlayerColor.White);
        Assert.Equal(PlayerColor.Empty, board1.GetPiece(0, 0));
        Assert.Equal(PlayerColor.White, board2.GetPiece(0, 0));
    }
}

public class PositionTests
{
    [Fact]
    public void Constructor_ValidPosition_Succeeds()
    {
        var pos = new Position(3, 4);
        Assert.Equal(3, pos.Row);
        Assert.Equal(4, pos.Column);
    }

    [Fact]
    public void Constructor_InvalidPosition_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Position(8, 5));
        Assert.Throws<ArgumentException>(() => new Position(-1, 3));
    }

    [Fact]
    public void Equals_SamePosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(3, 4);
        Assert.Equal(pos1, pos2);
    }

    [Fact]
    public void Equals_DifferentPosition_ReturnsFalse()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(4, 3);
        Assert.NotEqual(pos1, pos2);
    }
}

public class PlayerColorTests
{
    [Fact]
    public void Opponent_Black_ReturnsWhite()
    {
        Assert.Equal(PlayerColor.White, PlayerColor.Black.Opponent());
    }

    [Fact]
    public void Opponent_White_ReturnsBlack()
    {
        Assert.Equal(PlayerColor.Black, PlayerColor.White.Opponent());
    }

    [Fact]
    public void ToDisplayString_Black_ReturnsJapaneseText()
    {
        Assert.Equal("黒", PlayerColor.Black.ToDisplayString());
    }
}
