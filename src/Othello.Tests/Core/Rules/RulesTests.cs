namespace Technopro.Othello.Tests.Core.Rules;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

public class OthelloRulesTests
{
    /// <summary>
    /// 初期盤面で黒の有効手が正しく 4 件取得できることを確認する。
    /// パス条件: 件数が 4 かつ (3,2)(2,3)(4,5)(5,4) の 4 座標をすべて含むこと。
    /// </summary>
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

    /// <summary>
    /// 初期盤面において黒の有効手 (3,2) が IsValidMove で true と判定されることを確認する。
    /// パス条件: IsValidMove の戻り値が true であること。
    /// </summary>
    [Fact]
    public void IsValidMove_ValidPosition_ReturnsTrue()
    {
        var board = new Board();
        Assert.True(OthelloRules.IsValidMove(board, new Position(3, 2), PlayerColor.Black));
    }

    /// <summary>
    /// 石を挟めない座標（隅 (0,0)）は IsValidMove で false と判定されることを確認する。
    /// パス条件: IsValidMove の戻り値が false であること。
    /// </summary>
    [Fact]
    public void IsValidMove_InvalidPosition_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.IsValidMove(board, new Position(0, 0), PlayerColor.Black));
    }

    /// <summary>
    /// 有効手に着手すると盤面が更新され黒の石数が増加することを確認する。
    /// パス条件: MakeMove 後の CountPieces(Black) が着手前より大きいこと。
    /// </summary>
    [Fact]
    public void MakeMove_ValidMove_UpdatesBoard()
    {
        var board = new Board();
        int beforeCount = board.CountPieces(PlayerColor.Black);
        OthelloRules.MakeMove(board, new Position(3, 2), PlayerColor.Black);
        Assert.True(board.CountPieces(PlayerColor.Black) > beforeCount);
    }

    /// <summary>
    /// 初期盤面では黒に有効手があるためパスできない（CanPass = false）ことを確認する。
    /// パス条件: CanPass の戻り値が false であること。
    /// </summary>
    [Fact]
    public void CanPass_InitialBoard_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.CanPass(board, PlayerColor.Black));
    }

    /// <summary>
    /// 初期盤面では両者に有効手があるためゲームが終了していない（IsGameOver = false）ことを確認する。
    /// パス条件: IsGameOver の戻り値が false であること。
    /// </summary>
    [Fact]
    public void IsGameOver_InitialBoard_ReturnsFalse()
    {
        var board = new Board();
        Assert.False(OthelloRules.IsGameOver(board));
    }

    /// <summary>
    /// 盤面が完全に埋まっている場合、両者ともに有効手がなく IsGameOver = true になることを確認する。
    /// パス条件: 全マス黒の盤面で IsGameOver が true であること。
    /// </summary>
    [Fact]
    public void IsGameOver_FullBoard_ReturnsTrue()
    {
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.Black);

        Assert.True(OthelloRules.IsGameOver(board));
    }

    /// <summary>
    /// 自分の石を 1 枚も持たないプレイヤーは有効手がなく CanPass = true になることを確認する。
    /// パス条件: 全マス黒の盤面で白の CanPass が true、IsGameOver も true であること。
    /// </summary>
    [Fact]
    public void CanPass_WhenNoValidMoves_ReturnsTrue()
    {
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.Black);

        Assert.True(OthelloRules.CanPass(board, PlayerColor.White));
        Assert.True(OthelloRules.IsGameOver(board));
    }
}

public class FlipCalculatorTests
{
    /// <summary>
    /// 初期盤面で黒が (3,2) に打つと白 (3,3) を反転できることを確認する。
    /// パス条件: 反転リストが 1 件かつ (3,3) を含むこと。
    /// </summary>
    [Fact]
    public void GetFlippablePieces_ValidPosition_ReturnsFlippedPieces()
    {
        var board = new Board();
        var flipped = FlipCalculator.GetFlippablePieces(board, new Position(3, 2), PlayerColor.Black);
        Assert.Single(flipped);
        Assert.Contains(new Position(3, 3), flipped);
    }

    /// <summary>
    /// 石を挟めない座標（隅 (0,0)）では反転リストが空であることを確認する。
    /// パス条件: GetFlippablePieces の戻り値が空コレクションであること。
    /// </summary>
    [Fact]
    public void GetFlippablePieces_NoFlips_ReturnsEmpty()
    {
        var board = new Board();
        var flipped = FlipCalculator.GetFlippablePieces(board, new Position(0, 0), PlayerColor.Black);
        Assert.Empty(flipped);
    }
}
