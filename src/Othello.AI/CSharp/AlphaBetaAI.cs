namespace Technopro.Othello.Core.AI;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// αβプルーニング + Zobrist トランスポジションテーブルによる C# 純粋実装 AI。
/// Python が利用できない環境でのフォールバックとして使用する。
/// </summary>
public class AlphaBetaAI : IAIStrategy
{
	private static readonly ulong[,,] ZobristTable = InitZobristTable();

	/// <summary>
	/// TT エントリに格納するノード種別。
	/// Exact: αβ窓内の正確値。LowerBound: βカット（fail-high）で得た下界値。
	/// UpperBound: αを改善できなかった（fail-low）上界値。
	/// </summary>
	private enum NodeType { Exact, LowerBound, UpperBound }

	/// <summary>TT エントリ。Score・Depth・IsMaximizing に加えノード種別を保存する。</summary>
	private record struct TTEntry(int Score, int Depth, bool IsMaximizing, NodeType Type);
	private Dictionary<ulong, TTEntry>? _transpositionTable;

	public DifficultyLevel Difficulty { get; }
	public string EngineName => "AI: C#";

	/// <summary>
	/// 指定した難易度で AI を初期化する。
	/// </summary>
	/// <param name="difficulty">探索深さ・時間制限を決定する難易度。既定は <see cref="DifficultyLevel.Medium"/>。</param>
	public AlphaBetaAI(DifficultyLevel difficulty = DifficultyLevel.Medium)
	{
		Difficulty = difficulty;
	}

	private static ulong[,,] InitZobristTable()
	{
		// Zobrist ハッシュ用テーブル生成の決定論的乱数（Python/Rust 版と同じシード42での再現性のため）。
		// 暗号用途ではないため SCS0005（弱い乱数生成器）を明示的に許容する。
#pragma warning disable SCS0005
		var rng = new Random(42);
		var table = new ulong[Board.BoardSize, Board.BoardSize, 3];
		var bytes = new byte[8];
		for (int r = 0; r < Board.BoardSize; r++)
			for (int c = 0; c < Board.BoardSize; c++)
				for (int colorIdx = 0; colorIdx < 3; colorIdx++)
				{
					rng.NextBytes(bytes);
					table[r, c, colorIdx] = BitConverter.ToUInt64(bytes);
				}
#pragma warning restore SCS0005
		return table;
	}

	private static ulong ComputeBoardHash(Board board)
	{
		ulong hash = 0;
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
			{
				var piece = board.GetPiece(row, col);
				int colorIdx = piece == PlayerColor.Empty ? 0 : piece == PlayerColor.Black ? 1 : 2;
				hash ^= ZobristTable[row, col, colorIdx];
			}
		return hash;
	}

	/// <summary>
	/// 現在の難易度に応じた探索（固定深さ or 反復深化）で最善手を返す。
	/// </summary>
	/// <param name="board">現在の盤面。</param>
	/// <param name="playerColor">手を選ぶプレイヤーの色。</param>
	/// <returns>最善と判断した着手位置。</returns>
	public Position GetBestMove(Board board, PlayerColor playerColor)
	{
		var timeLimitMs = Difficulty.GetTimeLimitMs();
		return timeLimitMs.HasValue
			? GetBestMoveIterativeDeepening(board, playerColor, timeLimitMs.Value)
			: GetBestMoveFixedDepth(board, playerColor);
	}

	/// <summary>
	/// 時間制限付き反復深化探索（深さ 1 から <see cref="DifficultyLevel.GetSearchDepth"/> まで）。
	/// Hard 難易度の <see cref="GetBestMove"/> から呼ばれる。テストから直接呼び出し可能。
	/// </summary>
	internal Position GetBestMoveIterativeDeepening(Board board, PlayerColor playerColor, int timeLimitMs)
	{
		var validMoves = OthelloRules.GetValidMoves(board, playerColor);

		if (validMoves.Count == 0)
			throw new InvalidOperationException("有効な移動がありません");

		if (validMoves.Count == 1)
			return validMoves[0];

		var sortedMoves = SortMovesByHeuristic(validMoves);
		var bestMove = sortedMoves[0];
		int maxDepth = Difficulty.GetSearchDepth();
		var deadline = DateTime.UtcNow.AddMilliseconds(timeLimitMs);

		for (int depth = 1; depth <= maxDepth; depth++)
		{
			if (DateTime.UtcNow >= deadline)
				break;

			_transpositionTable = new Dictionary<ulong, TTEntry>(capacity: 1 << 16);

			var currentBest = sortedMoves[0];
			var bestScore = int.MinValue;
			var alpha = int.MinValue;
			bool timedOut = false;

			foreach (var move in sortedMoves)
			{
				if (DateTime.UtcNow >= deadline)
				{
					timedOut = true;
					break;
				}

				var newBoard = board.Clone();
				OthelloRules.MakeMove(newBoard, move, playerColor);

				try
				{
					var score = AlphaBeta(newBoard, depth - 1, alpha, int.MaxValue, false,
						playerColor.Opponent(), playerColor, deadline);

					if (score > bestScore)
					{
						bestScore = score;
						currentBest = move;
						alpha = bestScore;
					}
				}
				catch (TimeoutException)
				{
					// 探索途中でタイムアウト → この深さの結果を破棄し前の深さの結果を採用する
					timedOut = true;
					break;
				}
			}

			// 時間切れで深さが途中終了した場合は前の深さの結果を維持する
			if (!timedOut)
				bestMove = currentBest;
		}

		return bestMove;
	}

	private Position GetBestMoveFixedDepth(Board board, PlayerColor playerColor)
	{
		var validMoves = OthelloRules.GetValidMoves(board, playerColor);

		if (validMoves.Count == 0)
			throw new InvalidOperationException("有効な移動がありません");

		if (validMoves.Count == 1)
			return validMoves[0];

		int depth = Difficulty.GetSearchDepth();
		_transpositionTable = new Dictionary<ulong, TTEntry>(capacity: 1 << 16);

		var sortedMoves = SortMovesByHeuristic(validMoves);
		var bestScore = int.MinValue;
		var bestMove = sortedMoves[0];
		var alpha = int.MinValue;

		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.MakeMove(newBoard, move, playerColor);

			var score = AlphaBeta(newBoard, depth - 1, alpha, int.MaxValue, false,
				playerColor.Opponent(), playerColor);

			if (score > bestScore)
			{
				bestScore = score;
				bestMove = move;
				alpha = bestScore;
			}
		}

		return bestMove;
	}

	/// <summary>
	/// 深さ 0 またはゲーム終了時の終端評価を返す。非終端なら null。
	/// depth==0 を終局判定より先に見る。Python の _alpha_beta / Rust の alpha_beta_timed と同じ順序
	/// （depth==0 に達した葉は、たとえ終局していても通常の Evaluate を返す。Issue #95）。
	/// </summary>
	private static int? TryEvaluateTerminalNode(Board board, int depth, PlayerColor aiPlayer)
	{
		if (depth == 0)
			return Evaluator.Evaluate(board, aiPlayer);

		return OthelloRules.IsGameOver(board)
			? Evaluator.EvaluateFinal(board, aiPlayer, depth)  // depth で早い勝ちを選好（F5）
			: null;
	}

	/// <summary>
	/// 現プレイヤーに有効手がない場合の処理。
	/// TryEvaluateTerminalNode が null を返した後に到達するため、ゲームは終わっておらず
	/// 対戦相手には必ず有効手がある。isMaximizing を反転して対戦相手の手番に進む（F1）。
	/// </summary>
	private int HandleNoValidMoves(Board board, int depth, int alpha, int beta,
		bool isMaximizing, PlayerColor currentPlayer, PlayerColor aiPlayer, DateTime? deadline)
		=> AlphaBeta(board, depth - 1, alpha, beta, !isMaximizing,
			   currentPlayer.Opponent(), aiPlayer, deadline);

	private int EvaluateMaximizing(Board board, int depth, int alpha, int beta,
		PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves, DateTime? deadline)
	{
		var value = int.MinValue;
		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.MakeMove(newBoard, move, currentPlayer);
			value = Math.Max(value, AlphaBeta(newBoard, depth - 1, alpha, beta, false,
				currentPlayer.Opponent(), aiPlayer, deadline));
			alpha = Math.Max(alpha, value);
			if (beta <= alpha) break;
		}
		return value;
	}

	private int EvaluateMinimizing(Board board, int depth, int alpha, int beta,
		PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves, DateTime? deadline)
	{
		var value = int.MaxValue;
		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.MakeMove(newBoard, move, currentPlayer);
			value = Math.Min(value, AlphaBeta(newBoard, depth - 1, alpha, beta, true,
				currentPlayer.Opponent(), aiPlayer, deadline));
			beta = Math.Min(beta, value);
			if (beta <= alpha) break;
		}
		return value;
	}

	/// <summary>
	/// αβプルーニング探索の再帰本体。
	/// <paramref name="deadline"/> が指定されている場合、再帰の冒頭で超過を確認し
	/// 超過していれば <see cref="TimeoutException"/> を送出して即座に打ち切る
	/// （Python の _alpha_beta_timed / Rust の alpha_beta_timed と同じ設計）。
	/// null の場合（固定深さ探索）はこのチェックを行わない。
	/// </summary>
	private int AlphaBeta(Board board, int depth, int alpha, int beta, bool isMaximizing,
		PlayerColor currentPlayer, PlayerColor aiPlayer, DateTime? deadline = null)
	{
		if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
			throw new TimeoutException("AlphaBeta 探索が時間制限を超過しました");

		var hash = ComputeBoardHash(board);
		// isMaximizing を含めてコンテキストを検証する。
		// さらに NodeType を考慮して境界値を正確値として誤用しない。
		if (_transpositionTable!.TryGetValue(hash, out var tt)
			&& tt.Depth >= depth && tt.IsMaximizing == isMaximizing)
		{
			switch (tt.Type)
			{
				case NodeType.Exact:
					return tt.Score;
				case NodeType.LowerBound:
					alpha = Math.Max(alpha, tt.Score);
					break;
				case NodeType.UpperBound:
					beta = Math.Min(beta, tt.Score);
					break;
			}
			if (alpha >= beta) return tt.Score;
		}

		var terminalValue = TryEvaluateTerminalNode(board, depth, aiPlayer);
		if (terminalValue.HasValue)
		{
			_transpositionTable[hash] = new TTEntry(terminalValue.Value, depth, isMaximizing, NodeType.Exact);
			return terminalValue.Value;
		}

		var validMoves = OthelloRules.GetValidMoves(board, currentPlayer);
		if (validMoves.Count == 0)
		{
			var noMoveScore = HandleNoValidMoves(board, depth, alpha, beta, isMaximizing,
				currentPlayer, aiPlayer, deadline);
			_transpositionTable[hash] = new TTEntry(noMoveScore, depth, isMaximizing, NodeType.Exact);
			return noMoveScore;
		}

		var originalAlpha = alpha;
		var sortedMoves = SortMovesByHeuristic(validMoves);
		var result = isMaximizing
			? EvaluateMaximizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves, deadline)
			: EvaluateMinimizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves, deadline);

		// 探索後のスコアに基づいてノード種別を決定する
		var nodeType = result <= originalAlpha ? NodeType.UpperBound  // fail-low（上界値）
					 : result >= beta ? NodeType.LowerBound  // fail-high（下界値）
					 : NodeType.Exact;
		_transpositionTable[hash] = new TTEntry(result, depth, isMaximizing, nodeType);
		return result;
	}

	/// <summary>
	/// 位置ウェイトだけでソート（クローンなし）。
	/// 完全な盤面評価よりやや精度は落ちるが、クローンコストを排除して探索を高速化する（F6）。
	/// </summary>
	private static List<Position> SortMovesByHeuristic(List<Position> moves)
		=> moves
			.OrderByDescending(m => Evaluator.GetPositionWeight(m.Row, m.Column))
			.ToList();

	/// <summary>
	/// テスト専用: AlphaBeta() を直接呼び出す（deadline 超過検知の単体テスト用）。
	/// _transpositionTable の初期化も内部で行う。
	/// </summary>
	internal int AlphaBetaForTest(Board board, int depth, int alpha, int beta, bool isMaximizing,
		PlayerColor currentPlayer, PlayerColor aiPlayer, DateTime? deadline = null)
	{
		_transpositionTable = new Dictionary<ulong, TTEntry>();
		return AlphaBeta(board, depth, alpha, beta, isMaximizing, currentPlayer, aiPlayer, deadline);
	}
}
