namespace Technopro.Othello.Core.Rules;

using Technopro.Othello.Core.Models;

/// <summary>
/// 石を置いた際に反転される相手の石の座標を計算する静的クラス。
/// 8 方向（上下左右・斜め）に対して相手色の連続する石を探索し、
/// 自分の色で挟める場合に反転リストへ追加する。
/// </summary>
public static class FlipCalculator
{
    /// <summary>
    /// 探索する 8 方向のベクトル（dRow, dCol）。
    /// 上・下・左・右・左上・右上・左下・右下の順。
    /// </summary>
    private static readonly (int dRow, int dCol)[] Directions =
    [
        (-1, 0),  // 上
        ( 1, 0),  // 下
        ( 0,-1),  // 左
        ( 0, 1),  // 右
        (-1,-1),  // 左上
        (-1, 1),  // 右上
        ( 1,-1),  // 左下
        ( 1, 1)   // 右下
    ];

    /// <summary>
    /// 指定した position に playerColor の石を置いたとき、
    /// 8 方向すべてを探索して反転される石の座標リストを返す。
    /// </summary>
    /// <param name="board">現在の盤面（変更しない）</param>
    /// <param name="position">石を置こうとしているマス</param>
    /// <param name="playerColor">石を置くプレイヤーの色</param>
    /// <returns>反転される石の Position リスト。反転なしの場合は空リスト。</returns>
    public static List<Position> GetFlippablePieces(Board board, Position position, PlayerColor playerColor)
    {
        // 空きマスでなければ反転しない（既に石が置かれている）
        if (board.GetPiece(position) != PlayerColor.Empty)
            return [];

        var flipped = new List<Position>();
        var opponent = playerColor.Opponent();

        // 8 方向それぞれに反転可能な石を探索し、結果を集約する
        foreach (var (dRow, dCol) in Directions)
        {
            var piecesInDirection = GetFlipsInDirection(board, position, (dRow, dCol), playerColor, opponent);
            flipped.AddRange(piecesInDirection);
        }

        return flipped;
    }

    /// <summary>
    /// 指定した 1 方向について、反転できる相手の石の座標リストを返す。
    /// 相手色が連続した末尾に自分の色があれば、その途中の相手色をすべて返す。
    /// </summary>
    /// <param name="board">現在の盤面</param>
    /// <param name="start">起点となるマス（石を置く位置）</param>
    /// <param name="direction">探索方向（dRow, dCol）</param>
    /// <param name="playerColor">石を置くプレイヤーの色</param>
    /// <param name="opponent">相手プレイヤーの色（事前計算済み）</param>
    /// <returns>反転対象の石の座標リスト。反転できなければ空リスト。</returns>
    private static List<Position> GetFlipsInDirection(Board board, Position start,
        (int dRow, int dCol) direction, PlayerColor playerColor, PlayerColor opponent)
    {
        var row = start.Row + direction.dRow;
        var col = start.Column + direction.dCol;
        var flipped = new List<Position>();

        // 相手色の連続する石をスキャンする
        while (Position.IsValid(row, col))
        {
            var piece = board.GetPiece(row, col);

            if (piece == PlayerColor.Empty)
                // 空きマスに到達 → 挟めないため反転なし
                return [];

            if (piece == opponent)
            {
                // 相手の石 → 反転候補に追加して次のマスへ進む
                flipped.Add(new Position(row, col));
            }
            else if (piece == playerColor)
            {
                // 自分の石に到達 → 相手色を挟めたので反転リストを返す
                return flipped;
            }

            row += direction.dRow;
            col += direction.dCol;
        }

        // 盤端まで到達してしまった → 挟めないため反転なし
        return [];
    }
}
