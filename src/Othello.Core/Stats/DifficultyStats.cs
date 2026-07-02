namespace Technopro.Othello.Core.Stats;

/// <summary>
/// 特定難易度での対局統計。勝率・平均着手数を計算する。
/// </summary>
public class DifficultyStats
{
    /// <summary>勝利数</summary>
    public int Wins { get; set; }

    /// <summary>敗北数</summary>
    public int Losses { get; set; }

    /// <summary>引き分け数</summary>
    public int Draws { get; set; }

    /// <summary>累計着手数（全対局の合計）</summary>
    public int TotalMoves { get; set; }

    /// <summary>総ゲーム数（勝+負+引き分け）</summary>
    public int TotalGames => Wins + Losses + Draws;

    /// <summary>
    /// 勝率。引き分けを除いた勝利数 / (勝利数 + 敗北数)。
    /// 勝負が 0 件の場合は 0.0 を返す。
    /// </summary>
    public double WinRate => (Wins + Losses) > 0 ? (double)Wins / (Wins + Losses) : 0.0;

    /// <summary>
    /// 平均着手数。TotalGames が 0 の場合は 0.0 を返す。
    /// </summary>
    public double AverageMoves => TotalGames > 0 ? (double)TotalMoves / TotalGames : 0.0;
}
