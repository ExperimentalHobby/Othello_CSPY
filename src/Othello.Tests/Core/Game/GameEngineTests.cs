namespace Technopro.Othello.Tests.Core.Game;

using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

public class GameEngineTests
{
    /// <summary>
    /// Initialize() 後のゲーム状態が正しいことを確認する。
    /// パス条件: GameState が BlackTurn、黒・白ともに石数が 2 であること。
    /// </summary>
    [Fact]
    public void Initialize_StartsGame_WithCorrectState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    /// <summary>
    /// 有効な手を打つと着手成功かつターンが相手に移ることを確認する。
    /// パス条件: MakeMove の戻り値が IsSuccess = true、かつ GameState が WhiteTurn に変わること。
    /// </summary>
    [Fact]
    public void MakeMove_ValidMove_ChangesGameState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(3, 2));
        Assert.True(result.IsSuccess);
        Assert.Equal(GameState.WhiteTurn, engine.GameState);
    }

    /// <summary>
    /// 石を置けない位置（隅など）への着手は失敗を返すことを確認する。
    /// パス条件: MakeMove の戻り値が IsSuccess = false であること。
    /// </summary>
    [Fact]
    public void MakeMove_InvalidMove_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var result = engine.MakeMove(new Position(0, 0));
        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// 初期盤面における黒の有効手が 4 件であることを確認する。
    /// パス条件: GetValidMoves の結果が空でなく、件数が 4 であること。
    /// </summary>
    [Fact]
    public void GetValidMoves_ReturnsAvailableMoves()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var moves = engine.GetValidMoves(PlayerColor.Black);
        Assert.NotEmpty(moves);
        Assert.Equal(4, moves.Count);
    }

    /// <summary>
    /// 初期盤面では勝者が決まっていないことを確認する。
    /// パス条件: winner が null、黒・白ともに石数が 2 であること。
    /// </summary>
    [Fact]
    public void GetResult_InitialBoard_ReturnsNullWinner()
    {
        var engine = new GameEngine();
        engine.Initialize();
        var (winner, blackScore, whiteScore) = engine.GetResult();
        Assert.Null(winner);
        Assert.Equal(2, blackScore);
        Assert.Equal(2, whiteScore);
    }

    // --- Pass ---

    /// <summary>
    /// 有効手がある状態で Pass() を呼ぶと例外が投げられることを確認する。
    /// パス条件: InvalidOperationException がスローされること。
    /// </summary>
    [Fact]
    public void Pass_WhenValidMovesExist_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 初期盤面では黒（先手）に 4 手の有効手があるためパス不可
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    /// <summary>
    /// Initialize を呼ぶ前（GameState = Initialize の状態）で Pass() を呼ぶと例外が投げられることを確認する。
    /// パス条件: InvalidOperationException がスローされること。
    /// </summary>
    [Fact]
    public void Pass_BeforeInitialize_ThrowsInvalidOperationException()
    {
        var engine = new GameEngine();
        // Initialize せずに呼ぶ（GameState = Initialize → IsGameInProgress = false）
        Assert.Throws<InvalidOperationException>(() => engine.Pass());
    }

    // --- Undo ---

    /// <summary>
    /// 1 手打った後に Undo() すると、盤面・スコア・ターンが着手前の状態に戻ることを確認する。
    /// パス条件: Undo の戻り値が true、GameState が BlackTurn、黒・白ともに石数が 2 に戻ること。
    /// </summary>
    [Fact]
    public void Undo_AfterOneMove_RestoresPreviousState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 黒が (2,3) に打つ（初期盤面の有効手のひとつ）
        engine.MakeMove(new Position(2, 3));
        Assert.Equal(GameState.WhiteTurn, engine.GameState);

        var result = engine.Undo();

        Assert.True(result);
        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    /// <summary>
    /// 1 手も打っていない初期状態では Undo() が失敗することを確認する。
    /// パス条件: Undo の戻り値が false であること。
    /// </summary>
    [Fact]
    public void Undo_AtInitialState_ReturnsFalse()
    {
        var engine = new GameEngine();
        engine.Initialize();
        // 1 手も打っていない状態では Undo 不可
        Assert.False(engine.Undo());
    }

    /// <summary>
    /// 2 手打った後に Undo() を 2 回繰り返すと初期状態に戻ることを確認する。
    /// パス条件: GameState が BlackTurn、黒・白ともに石数が 2 に戻ること。
    /// </summary>
    [Fact]
    public void Undo_MultipleTimes_RestoresEarlierState()
    {
        var engine = new GameEngine();
        engine.Initialize();
        engine.MakeMove(new Position(2, 3)); // 黒
        engine.MakeMove(new Position(2, 2)); // 白
        engine.Undo(); // 白を戻す
        engine.Undo(); // 黒を戻す

        Assert.Equal(GameState.BlackTurn, engine.GameState);
        Assert.Equal(2, engine.BlackScore);
        Assert.Equal(2, engine.WhiteScore);
    }

    // --- 自動パス（AdvanceTurn のスキップ）/ パスをまたぐ Undo / 終局検出 ---

    /// <summary>
    /// 全マスを Black で埋め、指定座標のみ別の状態にしたテスト用盤面を生成する。
    /// </summary>
    private static Board BuildBoard(params (int row, int col, PlayerColor color)[] overrides)
    {
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.Black);
        foreach (var (row, col, color) in overrides)
            board.SetPiece(row, col, color);
        return board;
    }

    /// <summary>
    /// 着手後に相手が有効手を持たない場合、AdvanceTurn が相手を自動スキップし
    /// 手番が元のプレイヤーに戻る（強制パスが LastPassedPlayer に記録される）ことを確認する。
    /// パス条件: 黒の着手後も CurrentPlayer が黒のまま、LastPassedPlayer が白、GameState が BlackTurn であること。
    /// </summary>
    [Fact]
    public void MakeMove_OpponentHasNoMoves_SkipsOpponentAndRecordsPass()
    {
        // (0,0)/(7,7) が空き、(0,1)/(7,6) が白、その他すべて黒。
        // 白は石を挟めず有効手なし。黒は (0,0)・(7,7) に着手できる。
        var board = BuildBoard(
            (0, 0, PlayerColor.Empty), (0, 1, PlayerColor.White),
            (7, 7, PlayerColor.Empty), (7, 6, PlayerColor.White));

        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);

        var result = engine.MakeMove(new Position(0, 0)); // 黒が着手。白は依然パス、黒に (7,7) が残る

        Assert.True(result.IsSuccess);
        Assert.Equal(PlayerColor.Black, engine.CurrentPlayer);      // 白はスキップされ黒のまま
        Assert.Equal(PlayerColor.White, engine.LastPassedPlayer);   // 白が強制パスしたと記録
        Assert.Equal(GameState.BlackTurn, engine.GameState);
    }

    /// <summary>
    /// 相手がパスでスキップされた着手を Undo すると、手番が単純反転ではなく
    /// 正しく元のプレイヤーに戻ることを確認する（パスをまたぐ Undo の回帰テスト）。
    /// パス条件: Undo 後に CurrentPlayer が黒、(0,0) が空き・(0,1) が白に戻り、LastPassedPlayer が null であること。
    /// </summary>
    [Fact]
    public void Undo_AcrossOpponentPass_RestoresCorrectTurn()
    {
        var board = BuildBoard(
            (0, 0, PlayerColor.Empty), (0, 1, PlayerColor.White),
            (7, 7, PlayerColor.Empty), (7, 6, PlayerColor.White));

        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);
        engine.MakeMove(new Position(0, 0)); // 白スキップ → 黒の手番のまま

        bool undone = engine.Undo();

        Assert.True(undone);
        Assert.Equal(PlayerColor.Black, engine.CurrentPlayer);              // 白に戻さず黒のまま
        Assert.Equal(PlayerColor.Empty, engine.CurrentBoard.GetPiece(0, 0));
        Assert.Equal(PlayerColor.White, engine.CurrentBoard.GetPiece(0, 1));
        Assert.Null(engine.LastPassedPlayer);
    }

    /// <summary>
    /// 盤面を埋める最後の一手で両者が着手不能になり、石数に応じて終局状態になることを確認する。
    /// パス条件: 黒が最後のマスに着手後、GameState が BlackWon になること。
    /// </summary>
    [Fact]
    public void MakeMove_FillingLastCell_EndsGameWithWinner()
    {
        // (0,0) のみ空き、(0,1) が白、その他すべて黒。黒が (0,0) に置くと盤面が黒で埋まる。
        var board = BuildBoard(
            (0, 0, PlayerColor.Empty), (0, 1, PlayerColor.White));

        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);

        engine.MakeMove(new Position(0, 0));

        Assert.True(engine.GameState.IsGameOver());
        Assert.Equal(GameState.BlackWon, engine.GameState);
    }

    // --- GetResult 勝敗ケース ---

    /// <summary>
    /// 終局後に黒が勝利している盤面で GetResult が黒勝者と正しいスコアを返すことを確認する。
    /// パス条件: winner が Black かつ GameState が BlackWon であること。
    /// </summary>
    [Fact]
    public void GetResult_AfterBlackWins_ReturnsBlackWinner()
    {
        // (0,0) のみ空き・(0,1) が白→黒が着手で黒だらけの盤面になり黒勝ち
        var board = BuildBoard(
            (0, 0, PlayerColor.Empty), (0, 1, PlayerColor.White));
        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);
        engine.MakeMove(new Position(0, 0));

        var (winner, blackScore, whiteScore) = engine.GetResult();

        Assert.Equal(PlayerColor.Black, winner);
        Assert.True(blackScore > whiteScore);
        Assert.Equal(GameState.BlackWon, engine.GameState);
    }

    /// <summary>
    /// 全マスを白で埋めた盤面で GetResult が白勝者を返すことを確認する。
    /// パス条件: winner が White、blackScore=0、whiteScore=64 であること。
    /// </summary>
    [Fact]
    public void GetResult_AfterWhiteWins_ReturnsWhiteWinner()
    {
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.White);
        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);

        var (winner, blackScore, whiteScore) = engine.GetResult();

        Assert.Equal(PlayerColor.White, winner);
        Assert.Equal(0, blackScore);
        Assert.Equal(64, whiteScore);
    }

    /// <summary>
    /// 黒白が 32 枚ずつの盤面で GetResult が引き分け（winner = null）を返すことを確認する。
    /// パス条件: winner が null、blackScore=32、whiteScore=32 であること。
    /// </summary>
    [Fact]
    public void GetResult_AfterDraw_ReturnsNullWinner()
    {
        // 上半分（行 0-3）を黒、下半分（行 4-7）を白で埋めて 32-32 の引き分け盤面を作る
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, r < 4 ? PlayerColor.Black : PlayerColor.White);
        var engine = new GameEngine();
        engine.LoadStateForTest(board, PlayerColor.Black);

        var (winner, blackScore, whiteScore) = engine.GetResult();

        Assert.Null(winner);
        Assert.Equal(32, blackScore);
        Assert.Equal(32, whiteScore);
    }

    /// <summary>
    /// MakeMove が成功したとき FlippedPieces に反転した石の座標が格納されることを確認する。
    /// パス条件: 初期盤面で黒が (2,3) に打つと FlippedPieces に (3,3) が含まれること。
    /// </summary>
    [Fact]
    public void MakeMove_ValidMove_PopulatesFlippedPieces()
    {
        var engine = new GameEngine();
        engine.Initialize();

        var result = engine.MakeMove(new Position(2, 3));

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.FlippedPieces);
        Assert.Contains(new Position(3, 3), result.FlippedPieces);
    }
}
