namespace Technopro.Othello.Core.AI;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// ボード状態を評価するクラス。スコアが高いほど AI プレイヤーに有利。
/// </summary>
public static class Evaluator
{
	private const int OpeningEmptyThreshold = 44;
	private const int EndgameEmptyThreshold = 20;
	private const int OpeningMobilityMultiplier = 20;
	private const int MidgameMobilityMultiplier = 10;
	private const int MidgameStabilityMultiplier = 25;
	private const int MidgameFrontierMultiplier = 5;
	private const int EndgamePieceDifferenceMultiplier = 10;
	private const int EndgameMobilityMultiplier = 10;
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

	/// <summary>
	/// 位置ウェイト・Mobility・Stability・Frontier による総合評価。
	/// 空きマス数に応じてフェーズ（序盤/中盤/終盤）を切り替える。
	/// Python の evaluate(board, player) / Rust の evaluate と同じ仕様（Issue #78）:
	/// 序盤（空き&gt;44）: 位置重み + Mobility×20
	/// 中盤（20≤空き≤44）: 位置重み + Mobility×10 + Stability×25 + Frontier差×5
	/// 終盤（空き&lt;20）: 石数差×10 + 位置重み + Mobility×10
	/// </summary>
	public static int Evaluate(Board board, PlayerColor aiPlayer)
	{
		var opponent = aiPlayer.Opponent();

		int weightScore = 0;
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
			{
				var piece = board.GetPiece(row, col);
				if (piece == aiPlayer) weightScore += EvaluationWeights[row, col];
				else if (piece == opponent) weightScore -= EvaluationWeights[row, col];
			}

		// Mobility: リストを確保しない CountValidMoves を使う
		int mobility = OthelloRules.CountValidMoves(board, aiPlayer) - OthelloRules.CountValidMoves(board, opponent);

		int emptyCount = board.CountPieces(PlayerColor.Empty);

		if (emptyCount > OpeningEmptyThreshold)
			return weightScore + mobility * OpeningMobilityMultiplier;

		if (emptyCount < EndgameEmptyThreshold)
		{
			int aiPieces = board.CountPieces(aiPlayer);
			int opponentPieces = board.CountPieces(opponent);
			return (aiPieces - opponentPieces) * EndgamePieceDifferenceMultiplier
				+ weightScore + mobility * EndgameMobilityMultiplier;
		}

		int stabilityScore = (CountStable(board, aiPlayer) - CountStable(board, opponent)) * MidgameStabilityMultiplier;
		int frontierScore = (CountFrontier(board, opponent) - CountFrontier(board, aiPlayer)) * MidgameFrontierMultiplier;
		return weightScore + mobility * MidgameMobilityMultiplier + stabilityScore + frontierScore;
	}

	/// <summary>
	/// 指定位置の位置ウェイトを返す（Move Ordering 用、ボードクローン不要）。
	/// </summary>
	internal static int GetPositionWeight(int row, int col) => EvaluationWeights[row, col];

	/// <summary>フロンティア判定に用いる 8 方向。Python の _DIRS8 と同じ。</summary>
	private static readonly (int Dr, int Dc)[] FrontierDirections =
	{
		(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)
	};

	/// <summary>
	/// player の石のうち、空きマスに隣接している石（フロンティア）の数を返す。
	/// フロンティアは不安定性の代理指標。少ないほど守りやすい盤面とみなす。
	/// Python の count_frontier / Rust の count_frontier と同じアルゴリズム。
	/// </summary>
	internal static int CountFrontier(Board board, PlayerColor player)
	{
		int count = 0;
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
			{
				if (board.GetPiece(row, col) != player) continue;

				foreach (var (dr, dc) in FrontierDirections)
				{
					int nr = row + dr, nc = col + dc;
					if (Position.IsValid(nr, nc) && board.GetPiece(nr, nc) == PlayerColor.Empty)
					{
						count++;
						break;
					}
				}
			}
		return count;
	}

	/// <summary>安定石判定に用いる 4 軸（横・縦・斜め2方向）。Python の _AXES と同じ。</summary>
	private static readonly (int Dr, int Dc)[] StabilityAxes =
	{
		(0, 1), (1, 0), (1, 1), (1, -1)
	};

	/// <summary>
	/// (row, col) の (dr, dc) 半軸方向が安定しているかを返す。
	/// Python の _is_half_axis_stable と同じ 4 条件（いずれかを満たせば安定）:
	/// 1. 逆方向が即座に盤外 → 逆側からの挟み込みアンカーが存在できない
	/// 2. この方向が即座に盤外 → 端
	/// 3. この方向の隣接マスが安定石
	/// 4. この方向の全ラインに空きなし → 配置不能
	/// </summary>
	private static bool IsHalfAxisStable(Board board, bool[,] stable, int row, int col, int dr, int dc)
	{
		int oppRow = row - dr, oppCol = col - dc;
		if (!Position.IsValid(oppRow, oppCol)) return true;

		int nextRow = row + dr, nextCol = col + dc;
		if (!Position.IsValid(nextRow, nextCol)) return true;

		if (stable[nextRow, nextCol]) return true;

		int r = nextRow, c = nextCol;
		while (Position.IsValid(r, c))
		{
			if (board.GetPiece(r, c) == PlayerColor.Empty) return false;
			r += dr;
			c += dc;
		}
		return true;
	}

	/// <summary>4 軸のうち 1 軸について安定判定を行う（両半軸ともに安定なら true）。</summary>
	private static bool IsAxisStable(Board board, bool[,] stable, int row, int col, int dr, int dc)
		=> IsHalfAxisStable(board, stable, row, col, dr, dc) && IsHalfAxisStable(board, stable, row, col, -dr, -dc);

	/// <summary>
	/// player の安定石（絶対にひっくり返せない石）の数を返す。
	/// コーナー起点の flood-fill: 4 軸すべてで安定している石を安定石とし、変化がなくなるまで繰り返し伝播させる。
	/// Python の count_stable / Rust の count_stable と同じアルゴリズム。
	/// </summary>
	internal static int CountStable(Board board, PlayerColor player)
	{
		var stable = new bool[Board.BoardSize, Board.BoardSize];
		bool changed = true;
		while (changed)
		{
			changed = false;
			for (int row = 0; row < Board.BoardSize; row++)
				for (int col = 0; col < Board.BoardSize; col++)
				{
					if (stable[row, col] || board.GetPiece(row, col) != player) continue;

					bool allAxesStable = true;
					foreach (var (dr, dc) in StabilityAxes)
					{
						if (!IsAxisStable(board, stable, row, col, dr, dc))
						{
							allAxesStable = false;
							break;
						}
					}

					if (allAxesStable)
					{
						stable[row, col] = true;
						changed = true;
					}
				}
		}

		int count = 0;
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
				if (stable[row, col]) count++;
		return count;
	}

	/// <summary>
	/// 終局時の最終評価。
	/// depth（残り探索深さ）を加算して早い勝ちを選好・遅い負けを選好する。
	/// Python の evaluate_final(board, player, depth) と同じ挙動。
	/// </summary>
	public static int EvaluateFinal(Board board, PlayerColor aiPlayer, int depth = 0)
	{
		var (winner, _, _) = OthelloRules.GetGameResult(board);
		if (winner == aiPlayer) return VictoryScore + depth;
		if (winner == null) return DrawScore;
		return DefeatScore - depth;
	}
}
