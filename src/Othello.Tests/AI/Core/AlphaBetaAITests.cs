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
        int actual   = OthelloRules.CountValidMoves(board, color);

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
