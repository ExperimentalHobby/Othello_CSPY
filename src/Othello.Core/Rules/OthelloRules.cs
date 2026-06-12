namespace Technopro.Othello.Core.Rules;

using Technopro.Othello.Core.Models;

/// <summary>
/// オセロのゲームルールを実装する静的クラス。
/// 有効手判定・石の反転・パス判定・終局判定・勝敗判定を提供する。
/// 盤面（Board）を直接変更するメソッドと、変更しない参照専用メソッドで構成される。
/// </summary>
public static class OthelloRules
{
    /// <summary>
    /// 指定した position への着手が有効かどうかを判定する。
    /// 1 方向以上で相手の石を挟んで反転できる場合のみ有効とする。
    /// </summary>
    /// <param name="board">現在の盤面（変更しない）</param>
    /// <param name="position">着手しようとするマス</param>
    /// <param name="playerColor">着手するプレイヤーの色</param>
    /// <returns>着手が有効であれば true</returns>
    public static bool IsValidMove(Board board, Position position, PlayerColor playerColor)
    {
        // 既に石が置かれているマスへは着手できない
        if (board.GetPiece(position) != PlayerColor.Empty)
            return false;

        // 1 枚以上反転できる場合のみ有効
        var flipped = FlipCalculator.GetFlippablePieces(board, position, playerColor);
        return flipped.Count > 0;
    }

    /// <summary>
    /// 指定したプレイヤーが着手できる全有効マスの座標リストを返す。
    /// </summary>
    /// <param name="board">現在の盤面</param>
    /// <param name="playerColor">着手するプレイヤーの色</param>
    /// <returns>有効な着手位置の Position リスト（有効手がなければ空）</returns>
    public static List<Position> GetValidMoves(Board board, PlayerColor playerColor)
    {
        var validMoves = new List<Position>();
        // 空きマスをすべて試し、有効なものだけ収集する
        foreach (var position in board.GetEmptySquares())
        {
            if (IsValidMove(board, position, playerColor))
                validMoves.Add(position);
        }
        return validMoves;
    }

    /// <summary>
    /// 指定した position に playerColor の石を置き、挟んだ相手の石を反転する。
    /// 盤面を直接変更するため、呼び出し前に必ず IsValidMove で有効性を確認すること。
    /// </summary>
    /// <param name="board">変更対象の盤面</param>
    /// <param name="position">着手するマス</param>
    /// <param name="playerColor">着手するプレイヤーの色</param>
    /// <exception cref="InvalidOperationException">position への着手が無効な場合</exception>
    public static void MakeMove(Board board, Position position, PlayerColor playerColor)
    {
        // 事前条件チェック: 無効な着手は例外で弾く
        if (!IsValidMove(board, position, playerColor))
            throw new InvalidOperationException($"無効な移動: {position}");

        // 反転対象を計算してから石を置く
        var flipped = FlipCalculator.GetFlippablePieces(board, position, playerColor);
        board.SetPiece(position, playerColor);

        // 挟んだ相手の石を自分の色に変える
        foreach (var flippedPos in flipped)
            board.SetPiece(flippedPos, playerColor);
    }

    /// <summary>
    /// 指定したプレイヤーがパスしなければならない状態かどうかを判定する。
    /// 有効手が 1 つもない場合のみパス可能。
    /// </summary>
    /// <param name="board">現在の盤面</param>
    /// <param name="playerColor">判定対象のプレイヤー</param>
    /// <returns>有効手がなければ true（パス必須）</returns>
    public static bool CanPass(Board board, PlayerColor playerColor) =>
        GetValidMoves(board, playerColor).Count == 0;

    /// <summary>
    /// 指定したプレイヤーの有効手の件数を返す（リストを構築しない高速版）。
    /// Evaluator の Mobility 計算など、件数だけ必要な場面で使う。
    /// </summary>
    public static int CountValidMoves(Board board, PlayerColor playerColor)
    {
        int count = 0;
        foreach (var position in board.GetEmptySquares())
            if (IsValidMove(board, position, playerColor))
                count++;
        return count;
    }

    /// <summary>
    /// 両プレイヤーともに有効手がない（ゲーム終了）かどうかを判定する。
    /// どちらかに有効手があれば false を返す。
    /// </summary>
    /// <param name="board">現在の盤面</param>
    /// <returns>両者ともに着手不可能であれば true</returns>
    public static bool IsGameOver(Board board) =>
        GetValidMoves(board, PlayerColor.Black).Count == 0 &&
        GetValidMoves(board, PlayerColor.White).Count == 0;

    /// <summary>
    /// 終局時の勝者と双方の石数を返す。
    /// 石数の多い側が勝者。同数の場合は winner = null（引き分け）。
    /// </summary>
    /// <param name="board">終局後の盤面</param>
    /// <returns>（勝者の色 or null, 黒の石数, 白の石数）のタプル</returns>
    public static (PlayerColor? Winner, int BlackScore, int WhiteScore) GetGameResult(Board board)
    {
        int blackCount = board.CountPieces(PlayerColor.Black);
        int whiteCount = board.CountPieces(PlayerColor.White);

        if (blackCount > whiteCount)
            // 黒の方が多い → 黒の勝利
            return (PlayerColor.Black, blackCount, whiteCount);
        else if (whiteCount > blackCount)
            // 白の方が多い → 白の勝利
            return (PlayerColor.White, blackCount, whiteCount);
        else
            // 同数 → 引き分け（winner = null）
            return (null, blackCount, whiteCount);
    }
}
