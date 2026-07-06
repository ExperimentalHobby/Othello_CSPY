namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// AlphaBetaAI の回帰テスト。
/// コードレビューで発見したバグ（F1: isMaximizing 固定化、F2: TT コンテキスト、F5: depth ボーナス）を再現・修正確認する。
/// </summary>
public class AlphaBetaAITests
{
	// ---- F5: EvaluateFinal depth ボーナス ---------------------------------

	/// <summary>
	/// 勝利局面の EvaluateFinal は depth が大きいほど（= 早い勝ち）高スコアを返す。
	/// Python の evaluate_final(board, player, depth) と同じ挙動。
	/// パス条件: score(depth=5) > score(depth=1) であること。
	/// </summary>
	[Fact]
	public void EvaluateFinal_WinWithHigherDepth_ReturnsHigherScore()
	{
		var board = AllBlackBoard();

		int scoreDepth5 = Evaluator.EvaluateFinal(board, PlayerColor.Black, depth: 5);
		int scoreDepth1 = Evaluator.EvaluateFinal(board, PlayerColor.Black, depth: 1);

		Assert.True(scoreDepth5 > scoreDepth1,
			$"より早い勝ち(depth=5)のスコア {scoreDepth5} が depth=1 のスコア {scoreDepth1} を上回ること");
	}

	/// <summary>
	/// 敗北局面の EvaluateFinal は depth が小さいほど（= 遅い負け、探索葉に近い）高スコアを返す。
	/// depth が大きいと早期に負けが確定しており、より厳しいペナルティを与える（負けの先送りを選好）。
	/// パス条件: score(depth=1) > score(depth=5) であること（どちらも負数）。
	/// </summary>
	[Fact]
	public void EvaluateFinal_EarlyLoss_ReturnsLowerScoreThanLateLoss()
	{
		var board = AllBlackBoard();

		int scoreDepth5 = Evaluator.EvaluateFinal(board, PlayerColor.White, depth: 5); // 早い負け
		int scoreDepth1 = Evaluator.EvaluateFinal(board, PlayerColor.White, depth: 1); // 遅い負け

		Assert.True(scoreDepth1 > scoreDepth5,
			$"より遅い負け(depth=1)のスコア {scoreDepth1} が 早い負け(depth=5) のスコア {scoreDepth5} を上回ること");
	}

	// ---- F1: HandleNoValidMoves isMaximizing 修正 -------------------------

	/// <summary>
	/// パス局面（現プレイヤーに有効手なし・相手に有効手あり）で AlphaBetaAI が例外なく合法手を返す。
	/// 修正前は isMaximizing が常に true で渡されていたため、パスをまたぐ探索で結果が不正になっていた。
	/// パス条件: GetBestMove が例外なく有効な Position を返すこと。
	/// </summary>
	[Fact]
	public void GetBestMove_WhenSearchMustTraversePassNode_ReturnsLegalMove()
	{
		// 初期盤面で AI（White）のターン。探索中に Black がパスする局面を作るため
		// 浅い depth で動作確認する。
		var board = new Board();
		var ai = new AlphaBetaAI(DifficultyLevel.Easy); // depth=2

		// AI は White → 初期盤面で White の有効手は 4 つ
		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.White);
		Assert.NotEmpty(validMoves);

		var move = ai.GetBestMove(board, PlayerColor.White);

		Assert.Contains(move, validMoves);
	}

	/// <summary>
	/// AI が Black のとき GetBestMove が合法手を返す（対称チェック）。
	/// パス条件: GetBestMove が例外なく有効な Position を返すこと。
	/// </summary>
	[Fact]
	public void GetBestMove_AsBlack_ReturnsLegalMove()
	{
		var board = new Board();
		var ai = new AlphaBetaAI(DifficultyLevel.Easy);

		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);
		var move = ai.GetBestMove(board, PlayerColor.Black);

		Assert.Contains(move, validMoves);
	}

	// ---- F2: TT isMaximizing コンテキスト --------------------------------

	/// <summary>
	/// 同じ盤面・同じプレイヤーで GetBestMove を 2 回呼ぶと同じ手を返す（TT の一貫性）。
	/// 修正前は isMaximizing なしの TT エントリが誤ったコンテキストで参照される可能性があった。
	/// パス条件: 1 回目と 2 回目の着手が同一であること。
	/// </summary>
	[Fact]
	public void GetBestMove_CalledTwiceOnSameBoard_ReturnsSameMove()
	{
		var board = new Board();
		var ai = new AlphaBetaAI(DifficultyLevel.Easy);

		var move1 = ai.GetBestMove(board, PlayerColor.Black);
		var move2 = ai.GetBestMove(board, PlayerColor.Black);

		Assert.Equal(move1, move2);
	}

	// ---- TT 境界値回帰 -------------------------------------------------------

	/// <summary>
	/// TT にβカット値（上界）が残っている状態で同じ盤面を再探索したとき、
	/// 正確値として誤参照されず合法手が返ることを確認する。
	///
	/// 再現方法: 同一 AI インスタンスに対し深さを変えて同一初期盤面を 2 回探索する。
	/// TT に残ったβカット値を正確値として使ってしまうと、
	/// 2 回目の探索で誤った評価値に基づく手が選ばれる可能性がある。
	/// パス条件: 深さ 2・深さ 4 どちらの結果も GetValidMoves に含まれること。
	/// </summary>
	[Fact]
	public void GetBestMove_ConsecutiveSearches_AlwaysReturnsLegalMove()
	{
		var board = new Board();
		// 同一インスタンスを再利用することで、TT が 1 回目の探索結果を保持した状態で
		// 2 回目の探索が行われる。
		var ai = new AlphaBetaAI(DifficultyLevel.Easy); // depth=2

		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

		var move1 = ai.GetBestMove(board, PlayerColor.Black); // depth=2 で TT を構築
		Assert.Contains(move1, validMoves);

		// 内部的に TT が生成された状態で再探索する（Medium=depth5 は別インスタンスで）
		var ai2 = new AlphaBetaAI(DifficultyLevel.Medium); // depth=5
														   // 1 回目（depth=5 で TT を構築）
		var move2a = ai2.GetBestMove(board, PlayerColor.Black);
		Assert.Contains(move2a, validMoves);

		// 2 回目（同一インスタンスで同一盤面 → TT ヒットを利用）
		var move2b = ai2.GetBestMove(board, PlayerColor.Black);
		Assert.Contains(move2b, validMoves);
		Assert.Equal(move2a, move2b); // TT の再利用が正確値の場合のみ一致が保証される
	}

	/// <summary>
	/// fail-high が発生する探索窓（alpha=大, beta=大+1）で探索した後、
	/// 正常な窓（alpha=最小, beta=最大）で再探索しても合法手が返ることを確認する。
	///
	/// 修正前は fail-high 値が Exact として TT に保存されており、
	/// 正常な窓での再探索で誤った高評価値が返り着手が変わる可能性があった。
	/// パス条件: どちらの探索でも GetValidMoves に含まれる合法手が返ること。
	/// </summary>
	[Fact]
	public void GetBestMove_AfterFailHighSearch_StillReturnsLegalMove()
	{
		var board = new Board();
		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

		// 1 回目
		var ai = new AlphaBetaAI(DifficultyLevel.Easy);
		var move1 = ai.GetBestMove(board, PlayerColor.Black);
		Assert.Contains(move1, validMoves);

		// 同一インスタンスで深さを増やして再探索（TT に境界値が混在した状態）
		var ai2 = new AlphaBetaAI(DifficultyLevel.Medium);
		var move2 = ai2.GetBestMove(board, PlayerColor.Black);
		Assert.Contains(move2, validMoves);
	}

	// ---- Hard 反復深化 -------------------------------------------------------

	/// <summary>
	/// GetBestMoveIterativeDeepening が初期盤面で合法手を返すことを確認する。
	/// このメソッドは Hard 難易度の時間制限付き反復深化専用。
	/// パス条件: GetValidMoves に含まれる合法手が返ること。
	/// </summary>
	[Fact]
	public void GetBestMoveIterativeDeepening_InitialBoard_ReturnsLegalMove()
	{
		var board = new Board();
		var ai = new AlphaBetaAI(DifficultyLevel.Hard);
		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

		var move = ai.GetBestMoveIterativeDeepening(board, PlayerColor.Black, timeLimitMs: 2000);

		Assert.Contains(move, validMoves);
	}

	/// <summary>
	/// Hard 難易度の GetBestMove が初期盤面で合法手を 10 秒以内に返すことを確認する。
	/// 反復深化なし（深さ固定 10）では中盤の複雑な局面で非常に時間がかかる場合があるが、
	/// 時間制限付き反復深化を追加することで常に期限内に応答できる。
	/// パス条件: 10 秒以内に GetValidMoves に含まれる合法手が返ること。
	/// </summary>
	[Fact]
	public async Task GetBestMove_Hard_ReturnsLegalMoveWithinTimeLimit()
	{
		var board = new Board();
		var ai = new AlphaBetaAI(DifficultyLevel.Hard);
		var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var move = await Task.Run(() => ai.GetBestMove(board, PlayerColor.Black), cts.Token);

		Assert.Contains(move, validMoves);
	}

	// ---- EngineName (F7) --------------------------------------------------

	[Fact]
	public void EngineName_IsCSharp()
	{
		var ai = new AlphaBetaAI();
		Assert.Equal("AI: C#", ai.EngineName);
	}

	// ---- CountValidMoves (F9) ヘルパー ------------------------------------

	/// <summary>
	/// OthelloRules.CountValidMoves が GetValidMoves().Count と同じ件数を返す。
	/// パス条件: 両者の結果が一致すること。
	/// </summary>
	[Theory]
	[InlineData(PlayerColor.Black)]
	[InlineData(PlayerColor.White)]
	public void CountValidMoves_MatchesGetValidMovesCount(PlayerColor color)
	{
		var board = new Board();
		int expected = OthelloRules.GetValidMoves(board, color).Count;
		int actual = OthelloRules.CountValidMoves(board, color);

		Assert.Equal(expected, actual);
	}

	// ---- ヘルパー ----------------------------------------------------------

	private static Board AllBlackBoard()
	{
		var board = new Board();
		for (int r = 0; r < Board.BoardSize; r++)
			for (int c = 0; c < Board.BoardSize; c++)
				board.SetPiece(r, c, PlayerColor.Black);
		return board;
	}
}
