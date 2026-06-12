namespace Technopro.Othello.Core.AI;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// αβプルーニング + Zobrist トランスポジションテーブルによる C# 純粋実装 AI。
/// Python が利用できない環境でのフォールバックとして使用する。
/// </summary>
public class AlphaBetaAI : IAIStrategy
{
    private const int BoardSize = 8;

    private static readonly ulong[,,] ZobristTable = InitZobristTable();
    private Dictionary<ulong, (int Score, int Depth)>? _transpositionTable;

    public DifficultyLevel Difficulty { get; }

    public AlphaBetaAI(DifficultyLevel difficulty = DifficultyLevel.Medium)
    {
        Difficulty = difficulty;
    }

    private static ulong[,,] InitZobristTable()
    {
        var rng   = new Random(42);
        var table = new ulong[BoardSize, BoardSize, 3];
        var bytes = new byte[8];
        for (int r = 0; r < BoardSize; r++)
            for (int c = 0; c < BoardSize; c++)
                for (int colorIdx = 0; colorIdx < 3; colorIdx++)
                {
                    rng.NextBytes(bytes);
                    table[r, c, colorIdx] = BitConverter.ToUInt64(bytes);
                }
        return table;
    }

    private static ulong ComputeBoardHash(Board board)
    {
        ulong hash = 0;
        for (int row = 0; row < BoardSize; row++)
            for (int col = 0; col < BoardSize; col++)
            {
                var piece    = board.GetPiece(row, col);
                int colorIdx = piece == PlayerColor.Empty ? 0 : piece == PlayerColor.Black ? 1 : 2;
                hash ^= ZobristTable[row, col, colorIdx];
            }
        return hash;
    }

    public Position GetBestMove(Board board, PlayerColor playerColor)
    {
        var validMoves = OthelloRules.GetValidMoves(board, playerColor);

        if (validMoves.Count == 0)
            throw new InvalidOperationException("有効な移動がありません");

        if (validMoves.Count == 1)
            return validMoves[0];

        int depth = Difficulty.GetSearchDepth();
        _transpositionTable = new Dictionary<ulong, (int, int)>();

        var sortedMoves = SortMovesByHeuristic(board, validMoves, playerColor);
        var bestScore   = int.MinValue;
        var bestMove    = sortedMoves[0];
        var alpha       = int.MinValue;

        foreach (var move in sortedMoves)
        {
            var newBoard = board.Clone();
            OthelloRules.MakeMove(newBoard, move, playerColor);

            var score = AlphaBeta(newBoard, depth - 1, alpha, int.MaxValue, false,
                playerColor.Opponent(), playerColor);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove  = move;
                alpha     = bestScore;
            }
        }

        return bestMove;
    }

    private int? TryEvaluateTerminalNode(Board board, int depth, PlayerColor aiPlayer)
    {
        if (depth != 0 && !OthelloRules.IsGameOver(board))
            return null;

        return OthelloRules.IsGameOver(board)
            ? Evaluator.EvaluateFinal(board, aiPlayer)
            : Evaluator.Evaluate(board, aiPlayer);
    }

    private int HandleNoValidMoves(Board board, int depth, int alpha, int beta,
        PlayerColor currentPlayer, PlayerColor aiPlayer)
    {
        var opponent = currentPlayer.Opponent();
        if (!OthelloRules.CanPass(board, opponent))
        {
            return OthelloRules.IsGameOver(board)
                ? Evaluator.EvaluateFinal(board, aiPlayer)
                : Evaluator.Evaluate(board, aiPlayer);
        }
        return AlphaBeta(board, depth - 1, alpha, beta, true, opponent, aiPlayer);
    }

    private int EvaluateMaximizing(Board board, int depth, int alpha, int beta,
        PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves)
    {
        var value = int.MinValue;
        foreach (var move in sortedMoves)
        {
            var newBoard = board.Clone();
            OthelloRules.MakeMove(newBoard, move, currentPlayer);
            value = Math.Max(value, AlphaBeta(newBoard, depth - 1, alpha, beta, false,
                currentPlayer.Opponent(), aiPlayer));
            alpha = Math.Max(alpha, value);
            if (beta <= alpha) break;
        }
        return value;
    }

    private int EvaluateMinimizing(Board board, int depth, int alpha, int beta,
        PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves)
    {
        var value = int.MaxValue;
        foreach (var move in sortedMoves)
        {
            var newBoard = board.Clone();
            OthelloRules.MakeMove(newBoard, move, currentPlayer);
            value = Math.Min(value, AlphaBeta(newBoard, depth - 1, alpha, beta, true,
                currentPlayer.Opponent(), aiPlayer));
            beta = Math.Min(beta, value);
            if (beta <= alpha) break;
        }
        return value;
    }

    private int AlphaBeta(Board board, int depth, int alpha, int beta, bool isMaximizing,
        PlayerColor currentPlayer, PlayerColor aiPlayer)
    {
        var hash = ComputeBoardHash(board);
        if (_transpositionTable!.TryGetValue(hash, out var tt) && tt.Depth >= depth)
            return tt.Score;

        var terminalValue = TryEvaluateTerminalNode(board, depth, aiPlayer);
        if (terminalValue.HasValue)
        {
            _transpositionTable[hash] = (terminalValue.Value, depth);
            return terminalValue.Value;
        }

        var validMoves = OthelloRules.GetValidMoves(board, currentPlayer);
        if (validMoves.Count == 0)
        {
            var noMoveScore = HandleNoValidMoves(board, depth, alpha, beta, currentPlayer, aiPlayer);
            _transpositionTable[hash] = (noMoveScore, depth);
            return noMoveScore;
        }

        var sortedMoves = SortMovesByHeuristic(board, validMoves, currentPlayer);
        var result = isMaximizing
            ? EvaluateMaximizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves)
            : EvaluateMinimizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves);

        _transpositionTable[hash] = (result, depth);
        return result;
    }

    private static List<Position> SortMovesByHeuristic(Board board, List<Position> moves, PlayerColor playerColor)
    {
        return moves
            .Select(move =>
            {
                var testBoard = board.Clone();
                OthelloRules.MakeMove(testBoard, move, playerColor);
                return (move, score: Evaluator.EvaluatePositional(testBoard, playerColor));
            })
            .OrderByDescending(x => x.score)
            .Select(x => x.move)
            .ToList();
    }
}
