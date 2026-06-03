namespace Technopro.Othello.Tests.Core.Models;

using Technopro.Othello.Core.Models;

public class BoardTests
{
    /// <summary>
    /// Board 生成直後にオセロの初期配置（中央 4 マス）が正しく設定されることを確認する。
    /// パス条件: 黒・白の石数がそれぞれ 2 であること。
    /// </summary>
    [Fact]
    public void Constructor_CreatesBoard_WithInitialPieces()
    {
        var board = new Board();
        Assert.Equal(2, board.CountPieces(PlayerColor.Black));
        Assert.Equal(2, board.CountPieces(PlayerColor.White));
    }

    /// <summary>
    /// 初期配置の座標 (3,3)=白・(3,4)=黒 を正しく取得できることを確認する。
    /// パス条件: GetPiece(3,3) が White、GetPiece(3,4) が Black を返すこと。
    /// </summary>
    [Fact]
    public void GetPiece_ReturnsCorrectColor()
    {
        var board = new Board();
        Assert.Equal(PlayerColor.White, board.GetPiece(3, 3));
        Assert.Equal(PlayerColor.Black, board.GetPiece(3, 4));
    }

    /// <summary>
    /// SetPiece で石を置いた後、同座標を GetPiece で取得すると置いた色が返ることを確認する。
    /// パス条件: GetPiece(0,0) が SetPiece で指定した Black を返すこと。
    /// </summary>
    [Fact]
    public void SetPiece_UpdatesPosition()
    {
        var board = new Board();
        board.SetPiece(0, 0, PlayerColor.Black);
        Assert.Equal(PlayerColor.Black, board.GetPiece(0, 0));
    }

    /// <summary>
    /// Clone() が元の盤面と独立したコピーを返すことを確認する（一方を変更しても他方に影響しない）。
    /// パス条件: クローン側の変更が元の盤面に反映されず、元が Empty のままであること。
    /// </summary>
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
    /// <summary>
    /// 有効範囲（0〜7）の行・列で Position を生成すると正しい座標が設定されることを確認する。
    /// パス条件: Row が 3、Column が 4 であること。
    /// </summary>
    [Fact]
    public void Constructor_ValidPosition_Succeeds()
    {
        var pos = new Position(3, 4);
        Assert.Equal(3, pos.Row);
        Assert.Equal(4, pos.Column);
    }

    /// <summary>
    /// 範囲外（8以上・負値・両方無効）の座標で Position を生成すると例外が投げられることを確認する。
    /// パス条件: 行超過・列超過・行負値・列負値・両方超過のすべてで ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void Constructor_InvalidPosition_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Position(8, 5));
        Assert.Throws<ArgumentException>(() => new Position(-1, 3));
        // 行が有効・列が無効
        Assert.Throws<ArgumentException>(() => new Position(0, 8));
        Assert.Throws<ArgumentException>(() => new Position(3, -1));
        // 両方無効
        Assert.Throws<ArgumentException>(() => new Position(8, 8));
    }

    /// <summary>
    /// 同じ行・列を持つ 2 つの Position が等値と判定されることを確認する。
    /// パス条件: Assert.Equal が成功すること（Equals / == が true を返すこと）。
    /// </summary>
    [Fact]
    public void Equals_SamePosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(3, 4);
        Assert.Equal(pos1, pos2);
    }

    /// <summary>
    /// 行・列が異なる 2 つの Position が非等値と判定されることを確認する。
    /// パス条件: Assert.NotEqual が成功すること（Equals / == が false を返すこと）。
    /// </summary>
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
    /// <summary>
    /// Black.Opponent() が White を返すことを確認する。
    /// パス条件: 戻り値が PlayerColor.White であること。
    /// </summary>
    [Fact]
    public void Opponent_Black_ReturnsWhite()
    {
        Assert.Equal(PlayerColor.White, PlayerColor.Black.Opponent());
    }

    /// <summary>
    /// White.Opponent() が Black を返すことを確認する。
    /// パス条件: 戻り値が PlayerColor.Black であること。
    /// </summary>
    [Fact]
    public void Opponent_White_ReturnsBlack()
    {
        Assert.Equal(PlayerColor.Black, PlayerColor.White.Opponent());
    }

    /// <summary>
    /// Black.ToDisplayString() が日本語の "黒" を返すことを確認する。
    /// パス条件: 戻り値が "黒" であること。
    /// </summary>
    [Fact]
    public void ToDisplayString_Black_ReturnsJapaneseText()
    {
        Assert.Equal("黒", PlayerColor.Black.ToDisplayString());
    }
}
