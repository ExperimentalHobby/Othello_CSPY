namespace Technopro.Othello.Core.AI;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// ボード状態を評価するクラス。スコアが高いほど AI プレイヤーに有利。
/// </summary>
public static class Evaluator
{
    private const int BoardSize = 8;
    private const int EndgameThreshold = 50;
    private const int MidgameThreshold = 50;
    private const int PieceDifferenceMultiplier = 10;
    private const int MobilityMultiplier = 10;
    internal const int VictoryScore = 10000;
    internal const int DrawScore = 0;
    internal const int DefeatScore = -10000;

    private static readonly int[,] EvaluationWeights = new[,]
    {
        {  100, -20,  10,   5,   5,  10, -20,  100 },
        {  -20, -50,  -2,  -2,  -2,  -2, -50,  -20 },
        {   10,  -2,   5,   1,   1,   5,  -2,   10 },
        {    5,  -2,   1,   2,   2,   1,  -2,    5 },
        {    5,  -2,   1,   2,   2,   1,  -2,    5 },
        {   10,  -2,   5,   1,   1,   5,  -2,   10 },
        {  -20, -50,  -2,  -2,  -2,  -2, -50,  -20 },
        {  100, -20,  10,   5,   5,  10, -20,  100 }
    };

    /// <summary>位置ウェイト・石数・着手可能数による総合評価。</summary>
    public static int Evaluate(Board board, PlayerColor aiPlayer)
    {
        var opponent = aiPlayer.Opponent();
        int score = 0;

        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                var piece = board.GetPiece(row, col);
                if (piece == aiPlayer)       score += EvaluationWeights[row, col];
                else if (piece == opponent)  score -= EvaluationWeights[row, col];
            }

        int aiPieces       = board.CountPieces(aiPlayer);
        int opponentPieces = board.CountPieces(opponent);
        int totalPieces    = aiPieces + opponentPieces;

        if (totalPieces >= EndgameThreshold)
            score += (aiPieces - opponentPieces) * PieceDifferenceMultiplier;

        if (totalPieces < MidgameThreshold)
        {
            int aiMoves       = OthelloRules.GetValidMoves(board, aiPlayer).Count;
            int opponentMoves = OthelloRules.GetValidMoves(board, opponent).Count;
            score += (aiMoves - opponentMoves) * MobilityMultiplier;
        }

        return score;
    }

    /// <summary>位置ウェイトのみの高速評価（Move Ordering 用）。</summary>
    public static int EvaluatePositional(Board board, PlayerColor aiPlayer)
    {
        var opponent = aiPlayer.Opponent();
        int score = 0;
        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                var piece = board.GetPiece(row, col);
                if (piece == aiPlayer)       score += EvaluationWeights[row, col];
                else if (piece == opponent)  score -= EvaluationWeights[row, col];
            }
        return score;
    }

    /// <summary>終局時の最終評価。</summary>
    public static int EvaluateFinal(Board board, PlayerColor aiPlayer)
    {
        var (winner, _, _) = OthelloRules.GetGameResult(board);
        if (winner == aiPlayer) return VictoryScore;
        if (winner == null)     return DrawScore;
        return DefeatScore;
    }
}
