namespace Technopro.Othello.Tests.Helpers;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>テスト用の盤面を作成・検証するヘルパーメソッド集。</summary>
public static class TestBoardHelper
{
    /// <summary>
    /// 初期配置の新しい盤面を作成する。
    /// パス条件: 黒・白それぞれ 2 個が標準初期位置に配置された <see cref="Board"/> が返される。
    /// </summary>
    public static Board CreateInitialBoard() => new();

    /// <summary>
    /// 任意の駒配置で盤面を作成する。
    /// パス条件: 指定したすべての駒が正確に配置された <see cref="Board"/> が返される。
    /// </summary>
    public static Board CreateBoardWithPieces(params (int Row, int Col, PlayerColor Color)[] pieces)
    {
        var board = new Board();
        foreach (var (row, col, color) in pieces)
            board.SetPiece(row, col, color);
        return board;
    }

    /// <summary>
    /// テストの独立性を保つために盤面をクローンする。
    /// パス条件: 元の盤面と独立した（参照が異なる）コピーが返される。
    /// </summary>
    public static Board CloneBoard(Board board) => board.Clone();

    /// <summary>
    /// 指定色の駒数を返す。
    /// パス条件: 指定色の駒の総数が正確に返される。
    /// </summary>
    public static int GetPieceCount(Board board, PlayerColor color) => board.CountPieces(color);

    /// <summary>
    /// すべての期待駒が正しい位置・色で配置されているか検証する。
    /// パス条件: 全ての期待位置が一致すれば <c>true</c>、1 つでも異なれば <c>false</c>。
    /// </summary>
    public static bool VerifyPieces(Board board, params (Position Pos, PlayerColor Color)[] expected)
    {
        foreach (var (pos, color) in expected)
            if (board.GetPiece(pos) != color)
                return false;
        return true;
    }

    /// <summary>
    /// 空のマスのみからなる盤面を作成する（全マス <see cref="PlayerColor.Empty"/>）。
    /// パス条件: 石が 1 個も置かれていない盤面が返される。
    /// </summary>
    public static Board CreateEmptyBoard()
    {
        var board = new Board();
        for (int r = 0; r < Board.BoardSize; r++)
            for (int c = 0; c < Board.BoardSize; c++)
                board.SetPiece(r, c, PlayerColor.Empty);
        return board;
    }
}
