namespace Technopro.Othello.Core.AI;

using System.Diagnostics;
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

	/// <summary>
	/// TT のエントリ数上限の既定値。Expert 難易度（探索深さ12・時間制限15秒）のように
	/// 探索空間が大きいケースでは無制限に増え続けるとメモリ圧迫のリスクがあるため、
	/// 上限に到達したら全クリアする（Issue #164）。TT はあくまで探索高速化のためのキャッシュで
	/// あり、クリアしても探索結果の正しさには影響しない（再計算が発生するだけ）。
	/// </summary>
	private const int DefaultMaxTranspositionTableEntries = 2_000_000;

	private readonly int _maxTranspositionTableEntries;

	public DifficultyLevel Difficulty { get; }
	public string EngineName => "AI: C#";

	/// <summary>
	/// 指定した難易度で AI を初期化する。
	/// </summary>
	/// <param name="difficulty">探索深さ・時間制限を決定する難易度。既定は <see cref="DifficultyLevel.Medium"/>。</param>
	public AlphaBetaAI(DifficultyLevel difficulty = DifficultyLevel.Medium)
		: this(difficulty, DefaultMaxTranspositionTableEntries)
	{
	}

	/// <summary>テスト専用: TT のエントリ数上限を上書きできるコンストラクタ（Issue #164）。</summary>
	internal AlphaBetaAI(DifficultyLevel difficulty, int maxTranspositionTableEntries)
	{
		Difficulty = difficulty;
		_maxTranspositionTableEntries = maxTranspositionTableEntries;
	}

	/// <summary>テスト専用: 現在の TT のエントリ数を返す（Issue #164）。</summary>
	internal int TranspositionTableCountForTest => _transpositionTable?.Count ?? 0;

	/// <summary>
	/// TT へエントリを格納する。エントリ数が上限に達している場合は全クリアしてから格納する。
	/// </summary>
	private void StoreEntry(ulong hash, TTEntry entry)
	{
		if (_transpositionTable!.Count >= _maxTranspositionTableEntries)
			_transpositionTable.Clear();
		_transpositionTable[hash] = entry;
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
	/// 着手前の盤面ハッシュ（baseHash）から、着手後の盤面ハッシュを差分（XOR）更新で計算する。
	///
	/// 着手 1 手による盤面の差分は「着手位置が Empty→player に変わる」ことと
	/// 「flipped の各マスが 相手色→player に変わる」ことのみのため、ComputeBoardHash による
	/// O(64) の全面再計算をせず O(1 + flipped.Count) で求められる（XOR は自身が逆演算のため、
	/// 旧値を XOR で打ち消してから新値を XOR すれば更新になる。Issue #121）。
	/// </summary>
	private static ulong ComputeChildHash(ulong baseHash, Position position, PlayerColor player, List<Position> flipped)
	{
		int playerIdx = player == PlayerColor.Black ? 1 : 2;
		ulong hash = baseHash ^ ZobristTable[position.Row, position.Column, 0] ^ ZobristTable[position.Row, position.Column, playerIdx];

		int opponentIdx = playerIdx == 1 ? 2 : 1;
		foreach (var pos in flipped)
			hash ^= ZobristTable[pos.Row, pos.Column, opponentIdx] ^ ZobristTable[pos.Row, pos.Column, playerIdx];

		return hash;
	}

	/// <summary>テスト専用: ComputeBoardHash() を直接呼び出す（差分更新の正しさをフル計算と照合するため）。</summary>
	internal static ulong ComputeBoardHashForTest(Board board) => ComputeBoardHash(board);

	/// <summary>テスト専用: ComputeChildHash() を直接呼び出す。</summary>
	internal static ulong ComputeChildHashForTest(ulong baseHash, Position position, PlayerColor player, List<Position> flipped)
		=> ComputeChildHash(baseHash, position, player, flipped);

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
		// システム時刻（DateTime.UtcNow）は NTP 同期等で巻き戻り/進みうる非単調クロックのため、
		// 単調な Stopwatch ベースの時刻（Python の time.monotonic() / Rust の Instant と同種）を使う（Issue #163）。
		var deadline = Stopwatch.GetTimestamp() + MillisecondsToTicks(timeLimitMs);
		// ルート盤面のハッシュは 1 回だけフル計算し、以降は各候補手について
		// ComputeChildHash で差分更新する（Issue #121）。
		var rootHash = ComputeBoardHash(board);

		// 反復深化の全深さで 1 つの TT を使い回す（Issue #121）。
		// TT の各エントリは自身の残り探索深さ（Depth）を保持し、参照時に tt.Depth >= depth で
		// 十分な深さかどうかを判定しているため、深さをまたいで使い回しても意味論は変わらない
		// （より深い探索の結果が同じキーに後から上書きされるだけ）。
		_transpositionTable = new Dictionary<ulong, TTEntry>(capacity: 1 << 16);

		for (int depth = 1; depth <= maxDepth; depth++)
		{
			if (Stopwatch.GetTimestamp() >= deadline)
				break;

			var currentBest = sortedMoves[0];
			var bestScore = int.MinValue;
			var alpha = int.MinValue;
			bool timedOut = false;

			foreach (var move in sortedMoves)
			{
				if (Stopwatch.GetTimestamp() >= deadline)
				{
					timedOut = true;
					break;
				}

				var newBoard = board.Clone();
				OthelloRules.TryMakeMove(newBoard, move, playerColor, out var flipped);
				var newHash = ComputeChildHash(rootHash, move, playerColor, flipped);

				try
				{
					var score = AlphaBeta(newBoard, depth - 1, alpha, int.MaxValue, false,
						playerColor.Opponent(), playerColor, deadline, newHash);

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

	private Position GetBestMoveFixedDepth(Board board, PlayerColor playerColor) =>
		GetBestMoveAtDepth(board, playerColor, Difficulty.GetSearchDepth());

	/// <summary>
	/// テスト専用: DifficultyLevel を介さず、任意の探索深さで最善手を求める（Issue #162）。
	/// Python/Rust の golden データ（局面×任意深さ→期待着手）と直接照合するために使う。
	/// </summary>
	internal Position GetBestMoveAtDepthForTest(Board board, PlayerColor playerColor, int depth) =>
		GetBestMoveAtDepth(board, playerColor, depth);

	private Position GetBestMoveAtDepth(Board board, PlayerColor playerColor, int depth)
	{
		var validMoves = OthelloRules.GetValidMoves(board, playerColor);

		if (validMoves.Count == 0)
			throw new InvalidOperationException("有効な移動がありません");

		if (validMoves.Count == 1)
			return validMoves[0];

		_transpositionTable = new Dictionary<ulong, TTEntry>(capacity: 1 << 16);
		// ルート盤面のハッシュは 1 回だけフル計算し、以降は各候補手について
		// ComputeChildHash で差分更新する（Issue #121）。
		var rootHash = ComputeBoardHash(board);

		var sortedMoves = SortMovesByHeuristic(validMoves);
		var bestScore = int.MinValue;
		var bestMove = sortedMoves[0];
		var alpha = int.MinValue;

		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.TryMakeMove(newBoard, move, playerColor, out var flipped);
			var newHash = ComputeChildHash(rootHash, move, playerColor, flipped);

			var score = AlphaBeta(newBoard, depth - 1, alpha, int.MaxValue, false,
				playerColor.Opponent(), playerColor, boardHash: newHash);

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
	///
	/// Cacheable は呼び出し元が TT に格納してよいかを示す。EvaluateFinal は残り探索深さ depth に
	/// 依存するタイブレーク値を返すが、TT キーは depth を含まないため、パス回数が異なる経路で
	/// 同じ局面・同じ手番に到達すると誤った depth ボーナス値を再利用しうる（Issue #155）。
	/// そのため終局ノードはキャッシュ対象から除外する。
	/// </summary>
	private static (int Value, bool Cacheable)? TryEvaluateTerminalNode(Board board, int depth, PlayerColor aiPlayer)
	{
		if (depth == 0)
			return (Evaluator.Evaluate(board, aiPlayer), true);

		return OthelloRules.IsGameOver(board)
			? (Evaluator.EvaluateFinal(board, aiPlayer, depth), false)  // depth で早い勝ちを選好（F5）
			: null;
	}

	/// <summary>
	/// 現プレイヤーに有効手がない場合の処理。
	/// TryEvaluateTerminalNode が null を返した後に到達するため、ゲームは終わっておらず
	/// 対戦相手には必ず有効手がある。isMaximizing を反転して対戦相手の手番に進む（F1）。
	/// 盤面自体は変わらないため、ハッシュも hash をそのまま渡す（再計算不要。Issue #121）。
	/// </summary>
	private int HandleNoValidMoves(Board board, int depth, int alpha, int beta,
		bool isMaximizing, PlayerColor currentPlayer, PlayerColor aiPlayer, long? deadline, ulong hash)
		=> AlphaBeta(board, depth - 1, alpha, beta, !isMaximizing,
			   currentPlayer.Opponent(), aiPlayer, deadline, hash);

	private int EvaluateMaximizing(Board board, int depth, int alpha, int beta,
		PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves, long? deadline, ulong hash)
	{
		var value = int.MinValue;
		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.TryMakeMove(newBoard, move, currentPlayer, out var flipped);
			var newHash = ComputeChildHash(hash, move, currentPlayer, flipped);
			value = Math.Max(value, AlphaBeta(newBoard, depth - 1, alpha, beta, false,
				currentPlayer.Opponent(), aiPlayer, deadline, newHash));
			alpha = Math.Max(alpha, value);
			if (beta <= alpha) break;
		}
		return value;
	}

	private int EvaluateMinimizing(Board board, int depth, int alpha, int beta,
		PlayerColor currentPlayer, PlayerColor aiPlayer, List<Position> sortedMoves, long? deadline, ulong hash)
	{
		var value = int.MaxValue;
		foreach (var move in sortedMoves)
		{
			var newBoard = board.Clone();
			OthelloRules.TryMakeMove(newBoard, move, currentPlayer, out var flipped);
			var newHash = ComputeChildHash(hash, move, currentPlayer, flipped);
			value = Math.Min(value, AlphaBeta(newBoard, depth - 1, alpha, beta, true,
				currentPlayer.Opponent(), aiPlayer, deadline, newHash));
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
	/// <paramref name="boardHash"/> は呼び出し元が ComputeChildHash で差分計算した board の
	/// ハッシュ値（Issue #121）。探索の再帰呼び出しは常に指定する。省略時（null）は
	/// ComputeBoardHash でフル計算するフォールバックで、AlphaBetaForTest など board のみを
	/// 渡す呼び出し向け。
	/// </summary>
	private int AlphaBeta(Board board, int depth, int alpha, int beta, bool isMaximizing,
		PlayerColor currentPlayer, PlayerColor aiPlayer, long? deadline = null, ulong? boardHash = null)
	{
		if (deadline.HasValue && Stopwatch.GetTimestamp() >= deadline.Value)
			throw new TimeoutException("AlphaBeta 探索が時間制限を超過しました");

		var hash = boardHash ?? ComputeBoardHash(board);
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

		var terminal = TryEvaluateTerminalNode(board, depth, aiPlayer);
		if (terminal.HasValue)
		{
			if (terminal.Value.Cacheable)
				StoreEntry(hash, new TTEntry(terminal.Value.Value, depth, isMaximizing, NodeType.Exact));
			return terminal.Value.Value;
		}

		var validMoves = OthelloRules.GetValidMoves(board, currentPlayer);
		if (validMoves.Count == 0)
		{
			var noMoveScore = HandleNoValidMoves(board, depth, alpha, beta, isMaximizing,
				currentPlayer, aiPlayer, deadline, hash);
			// パスノードも通常の分岐ノードと同じ判定式で NodeType を決定する。
			// 子の呼び出しは同じ alpha/beta 窓をそのまま引き継ぐため、子側で fail-high/fail-low が
			// 起きれば境界値を返しうる。無条件に Exact とすると不正確な値を正確値として
			// 再利用してしまう（Issue #155）。
			var passNodeType = noMoveScore <= alpha ? NodeType.UpperBound
							  : noMoveScore >= beta ? NodeType.LowerBound
							  : NodeType.Exact;
			StoreEntry(hash, new TTEntry(noMoveScore, depth, isMaximizing, passNodeType));
			return noMoveScore;
		}

		var originalAlpha = alpha;
		var sortedMoves = SortMovesByHeuristic(validMoves);
		var result = isMaximizing
			? EvaluateMaximizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves, deadline, hash)
			: EvaluateMinimizing(board, depth, alpha, beta, currentPlayer, aiPlayer, sortedMoves, deadline, hash);

		// 探索後のスコアに基づいてノード種別を決定する
		var nodeType = result <= originalAlpha ? NodeType.UpperBound  // fail-low（上界値）
					 : result >= beta ? NodeType.LowerBound  // fail-high（下界値）
					 : NodeType.Exact;
		StoreEntry(hash, new TTEntry(result, depth, isMaximizing, nodeType));
		return result;
	}

	/// <summary>
	/// ミリ秒を Stopwatch.GetTimestamp() と同じ単位（タイマー刻み数）に変換する（Issue #163）。
	/// </summary>
	private static long MillisecondsToTicks(int milliseconds) =>
		(long)(milliseconds / 1000.0 * Stopwatch.Frequency);

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
		PlayerColor currentPlayer, PlayerColor aiPlayer, long? deadline = null)
	{
		_transpositionTable = new Dictionary<ulong, TTEntry>();
		return AlphaBeta(board, depth, alpha, beta, isMaximizing, currentPlayer, aiPlayer, deadline);
	}

	/// <summary>
	/// テスト専用: 指定 hash の TT エントリを検証用に公開する（Issue #155）。
	/// NodeType は private な列挙型のため文字列化して返す。エントリが無ければ null。
	/// </summary>
	internal (int Score, int Depth, string NodeType)? PeekTranspositionTableEntryForTest(ulong hash) =>
		_transpositionTable != null && _transpositionTable.TryGetValue(hash, out var entry)
			? (entry.Score, entry.Depth, entry.Type.ToString())
			: null;
}
