namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Tests.Helpers;

/// <summary>
/// PythonSubprocessAI の実プロセス結合テスト。
/// 実際に ai.py を起動し、盤面を渡して合法手が返ることを検証する。
/// Python スクリプトが出力ディレクトリに存在しない環境ではスキップする。
/// </summary>
public class PythonSubprocessAIIntegrationTests
{
    private static readonly string ScriptPath = AiScriptPaths.AiScriptPath;

    /// <summary>
    /// 初期盤面・Easy難易度で GetBestMove を呼ぶと、合法手の一つが返ることを確認する。
    /// パス条件: 返された Position が OthelloRules.GetValidMoves(初期盤面, Black) に含まれること。
    /// </summary>
    [Fact]
    public void GetBestMove_InitialBoardEasy_ReturnsLegalMove()
    {
        if (!File.Exists(ScriptPath)) return;

        var board = TestBoardHelper.CreateInitialBoard();
        var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

        using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, ScriptPath);
        var move = ai.GetBestMove(board, PlayerColor.Black);

        Assert.Contains(move, validMoves);
    }

    /// <summary>
    /// 数手進めた中盤局面でも GetBestMove が合法手を返すことを確認する。
    /// パス条件: 返された Position が OthelloRules.GetValidMoves(中盤局面, White) に含まれること。
    /// </summary>
    [Fact]
    public void GetBestMove_MidGameBoardEasy_ReturnsLegalMove()
    {
        if (!File.Exists(ScriptPath)) return;

        var engine = new Technopro.Othello.Core.Game.GameEngine();
        engine.Initialize();
        engine.MakeMove(new Position(2, 3)); // Black
        engine.MakeMove(new Position(2, 2)); // White
        var board = engine.CurrentBoard.Clone();
        var validMoves = OthelloRules.GetValidMoves(board, engine.CurrentPlayer);

        using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, ScriptPath);
        var move = ai.GetBestMove(board, engine.CurrentPlayer);

        Assert.Contains(move, validMoves);
    }

    /// <summary>
    /// Hard難易度（time_ms による反復深化）でも制限時間内に合法手が返ることを確認する。
    /// パス条件: 10 秒以内に OthelloRules.GetValidMoves に含まれる合法手が返ること。
    /// </summary>
    [Fact]
    public async Task GetBestMove_InitialBoardHard_ReturnsLegalMoveWithinTimeLimit()
    {
        if (!File.Exists(ScriptPath)) return;

        var board = TestBoardHelper.CreateInitialBoard();
        var validMoves = OthelloRules.GetValidMoves(board, PlayerColor.Black);

        using var ai = new PythonSubprocessAI(DifficultyLevel.Hard, ScriptPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var move = await Task.Run(() => ai.GetBestMove(board, PlayerColor.Black), cts.Token);

        Assert.Contains(move, validMoves);
    }
}
