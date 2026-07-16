namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Tests.Helpers;

/// <summary>
/// Evaluator の評価関数（フェーズ切替・安定石・フロンティア）のテスト。
/// Python (evaluator.py) / Rust (lib.rs) と同一仕様であることを確認する（Issue #78）。
/// </summary>
public class EvaluatorTests
{
	// ---- CountStable ------------------------------------------------------

	/// <summary>
	/// 四隅の石はどのような盤面状態でも安定石としてカウントされる。
	/// コーナーは 4 軸すべてで「盤外アンカーなし」または「即座に盤外」の条件を満たすため
	/// 他の石の状態に関わらず安定する（evaluator.count_stable と同じアルゴリズム）。
	/// パス条件: CountStable が 4 隅すべてを安定石として数え、合計 4 を返すこと。
	/// </summary>
	[Fact]
	public void CountStable_CornersOnly_AllFourAreCountedAsStable()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		board.SetPiece(0, 0, PlayerColor.Black);
		board.SetPiece(0, 7, PlayerColor.Black);
		board.SetPiece(7, 0, PlayerColor.Black);
		board.SetPiece(7, 7, PlayerColor.Black);

		int stableCount = Evaluator.CountStable(board, PlayerColor.Black);

		Assert.Equal(4, stableCount);
	}

	/// <summary>
	/// コーナーではない辺の石で、周囲が空きマスの場合は安定石としてカウントされない。
	/// (0,3) の水平軸は、右方向の隣接マス (0,4) が空きのため条件4（ラインに空きなし）を満たさず
	/// 半軸が不安定になり、軸全体・石全体が不安定と判定される。
	/// パス条件: CountStable が 0 を返すこと。
	/// </summary>
	[Fact]
	public void CountStable_NonCornerEdgeStoneSurroundedByEmpty_NotCountedAsStable()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		board.SetPiece(0, 3, PlayerColor.Black);

		int stableCount = Evaluator.CountStable(board, PlayerColor.Black);

		Assert.Equal(0, stableCount);
	}

	// ---- CountFrontier ------------------------------------------------------

	/// <summary>
	/// 空きマスに隣接する石はフロンティアとしてカウントされる。
	/// パス条件: 周囲 8 マスがすべて空きの単独石で CountFrontier が 1 を返すこと。
	/// </summary>
	[Fact]
	public void CountFrontier_StoneAdjacentToEmptySquare_IsCounted()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		board.SetPiece(3, 3, PlayerColor.Black);

		int frontierCount = Evaluator.CountFrontier(board, PlayerColor.Black);

		Assert.Equal(1, frontierCount);
	}

	/// <summary>
	/// 空きマスに一切隣接しない（周囲 8 マスすべてが石で埋まっている）石はフロンティアとしてカウントされない。
	/// パス条件: CountFrontier が 0 を返すこと。
	/// </summary>
	[Fact]
	public void CountFrontier_StoneFullySurroundedByOtherStones_NotCounted()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		board.SetPiece(3, 3, PlayerColor.Black);
		foreach (var (dr, dc) in new (int, int)[] { (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) })
			board.SetPiece(3 + dr, 3 + dc, PlayerColor.White);

		int frontierCount = Evaluator.CountFrontier(board, PlayerColor.Black);

		Assert.Equal(0, frontierCount);
	}

	// ---- Evaluate: フェーズ切替 ------------------------------------------------------

	/// <summary>
	/// 序盤（空きマス &gt; 44）は位置重み + Mobility × 20 で評価される。
	/// 初期盤面（空き60）に石を2つ追加しても空き58 &gt; 44 のため序盤フェーズを維持する。
	/// パス条件: Evaluate の戻り値が「位置重みスコア + Mobility差 × 20」と一致すること。
	/// </summary>
	[Fact]
	public void Evaluate_OpeningPhase_UsesMobilityMultiplier20()
	{
		var board = TestBoardHelper.CreateInitialBoard();
		board.SetPiece(2, 3, PlayerColor.Black);
		board.SetPiece(5, 4, PlayerColor.White);

		int expectedWeight = ComputeWeightScore(board, PlayerColor.Black);
		int expectedMobility = OthelloRules.CountValidMoves(board, PlayerColor.Black)
			- OthelloRules.CountValidMoves(board, PlayerColor.White);
		int expected = expectedWeight + expectedMobility * 20;

		Assert.Equal(expected, Evaluator.Evaluate(board, PlayerColor.Black));
	}

	/// <summary>
	/// 終盤（空きマス &lt; 20）は 石数差×10 + 位置重み + Mobility×10 で評価される（Mobility も含む点が旧仕様と異なる）。
	/// 唯一の空きマス (2,3) を除く全マスを埋め、(2,3) からの 8 方向を手動検証済み:
	/// 下方向 (3,3)=White→(4,3)=Black で Black のみ有効手 1 個、White は有効手 0 個。
	/// パス条件: Evaluate の戻り値が「石数差×10 + 位置重みスコア + Mobility差×10」と一致すること。
	/// </summary>
	[Fact]
	public void Evaluate_EndgamePhase_UsesPieceDifferenceMultiplier10AndIncludesMobility()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
				board.SetPiece(row, col, PlayerColor.Black);

		// (2,3) を挟む 8 方向のうち、White 側の非対称配置を作る（詳細はテストの summary を参照）
		foreach (var (r, c) in new (int, int)[] { (3, 3), (1, 3), (0, 3), (1, 2), (0, 1), (1, 4), (0, 5) })
			board.SetPiece(r, c, PlayerColor.White);

		board.SetPiece(2, 3, PlayerColor.Empty);

		int expectedWeight = ComputeWeightScore(board, PlayerColor.Black);
		int aiPieces = board.CountPieces(PlayerColor.Black);
		int opponentPieces = board.CountPieces(PlayerColor.White);
		int expectedMobility = OthelloRules.CountValidMoves(board, PlayerColor.Black)
			- OthelloRules.CountValidMoves(board, PlayerColor.White);
		int expected = (aiPieces - opponentPieces) * 10 + expectedWeight + expectedMobility * 10;

		Assert.Equal(expected, Evaluator.Evaluate(board, PlayerColor.Black));
	}

	/// <summary>
	/// 中盤（20≤空きマス≤44）は 位置重み + Mobility×10 + Stability×25 + Frontier差×5 で評価される。
	/// 旧仕様には存在しなかった Stability・Frontier 項が含まれることを確認する。
	/// パス条件: Evaluate の戻り値が「位置重み + Mobility×10 + Stability差×25 + Frontier差×5」と一致すること。
	/// </summary>
	[Fact]
	public void Evaluate_MidgamePhase_IncludesStabilityAndFrontierScores()
	{
		var board = TestBoardHelper.CreateEmptyBoard();
		for (int col = 0; col < Board.BoardSize; col++)
			for (int row = 0; row <= 2; row++)
				board.SetPiece(row, col, PlayerColor.Black); // 24 石
		for (int col = 0; col < Board.BoardSize; col++)
			board.SetPiece(5, col, PlayerColor.White); // 8 石
		// 占有 32、空き 32（20≤32≤44 で中盤フェーズ）

		int expectedWeight = ComputeWeightScore(board, PlayerColor.Black);
		int expectedMobility = OthelloRules.CountValidMoves(board, PlayerColor.Black)
			- OthelloRules.CountValidMoves(board, PlayerColor.White);
		int expectedStability = (Evaluator.CountStable(board, PlayerColor.Black)
			- Evaluator.CountStable(board, PlayerColor.White)) * 25;
		int expectedFrontier = (Evaluator.CountFrontier(board, PlayerColor.White)
			- Evaluator.CountFrontier(board, PlayerColor.Black)) * 5;
		int expected = expectedWeight + expectedMobility * 10 + expectedStability + expectedFrontier;

		Assert.Equal(expected, Evaluator.Evaluate(board, PlayerColor.Black));
	}

	/// <summary>位置重みスコア（AI 石の重み合計 − 相手石の重み合計）を計算するテスト用ヘルパー。</summary>
	private static int ComputeWeightScore(Board board, PlayerColor aiPlayer)
	{
		var opponent = aiPlayer.Opponent();
		int score = 0;
		for (int row = 0; row < Board.BoardSize; row++)
			for (int col = 0; col < Board.BoardSize; col++)
			{
				var piece = board.GetPiece(row, col);
				if (piece == aiPlayer) score += Evaluator.GetPositionWeight(row, col);
				else if (piece == opponent) score -= Evaluator.GetPositionWeight(row, col);
			}
		return score;
	}
}
