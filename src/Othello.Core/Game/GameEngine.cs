namespace Technopro.Othello.Core.Game;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// オセロゲーム全体の進行を制御するエンジンクラス。
/// 盤面・現在プレイヤー・ゲーム状態・履歴（Undo 用）を管理し、
/// 着手・パス・Undo・ゲーム終了判定のロジックを提供する。
/// </summary>
public class GameEngine
{
    /// <summary>現在の盤面状態</summary>
    private Board _board = new();

    /// <summary>ゲームの進行状態（Initialize / BlackTurn / WhiteTurn / 終了系）</summary>
    private GameState _gameState = GameState.Initialize;

    /// <summary>現在着手すべきプレイヤーの色</summary>
    private PlayerColor _currentPlayer = PlayerColor.Black;

    /// <summary>
    /// Undo 用の盤面スナップショット履歴。
    /// 各着手後に Board.Clone() を積んでいく。Initialize 時の初期盤面も含む。
    /// </summary>
    private List<Board> _history = new();

    /// <summary>
    /// 連続パスのカウンター。
    /// 両プレイヤーが連続で 2 回パスするとゲーム終了となる。
    /// 石を置いた際は 0 にリセットする。
    /// </summary>
    private int _passCount = 0;

    /// <summary>現在の盤面（読み取り専用）</summary>
    public Board CurrentBoard => _board;

    /// <summary>現在のゲーム進行状態</summary>
    public GameState GameState => _gameState;

    /// <summary>現在着手すべきプレイヤーの色</summary>
    public PlayerColor CurrentPlayer => _currentPlayer;

    /// <summary>黒の現在の石数</summary>
    public int BlackScore => _board.CountPieces(PlayerColor.Black);

    /// <summary>白の現在の石数</summary>
    public int WhiteScore => _board.CountPieces(PlayerColor.White);

    /// <summary>
    /// ゲームを初期状態に設定する。
    /// 盤面・履歴・パスカウントをリセットし、黒の先手でゲームを開始する。
    /// </summary>
    public void Initialize()
    {
        _board = new();
        _gameState = GameState.BlackTurn;
        _currentPlayer = PlayerColor.Black;
        _history.Clear();
        _passCount = 0;
        // 初期盤面を履歴の先頭として保存する（Undo の下限）
        _history.Add(_board.Clone());
    }

    /// <summary>
    /// 現在のプレイヤーが指定した position に石を置く。
    /// 成功時は相手のターンに進め、失敗時は盤面を変更しない。
    /// </summary>
    /// <param name="position">石を置くマスの座標</param>
    /// <returns>着手の結果（成功/失敗・メッセージ・反転した石リスト）</returns>
    public MoveResult MakeMove(Position position)
    {
        // ゲームが進行中でなければ着手できない
        if (!_gameState.IsGameInProgress())
            return MoveResult.Failure("ゲームが進行中ではありません");

        // 有効手でない場合は盤面を変更せずに失敗を返す
        if (!OthelloRules.IsValidMove(_board, position, _currentPlayer))
            return MoveResult.Failure("その位置は有効な移動ではありません");

        // 反転する石を計算してから盤面に反映する
        var flipped = FlipCalculator.GetFlippablePieces(_board, position, _currentPlayer);
        OthelloRules.MakeMove(_board, position, _currentPlayer);

        // 石を置いたのでパスカウントをリセット
        _passCount = 0;
        // 着手後の盤面を履歴に保存する
        _history.Add(_board.Clone());

        AdvanceTurn();
        return MoveResult.Success("移動に成功しました", flipped);
    }

    /// <summary>
    /// 現在のプレイヤーがパスする（有効手がない場合のみ呼び出し可能）。
    /// パスカウントが 2 に達した場合は両者パス扱いでゲームを終了する。
    /// </summary>
    /// <exception cref="InvalidOperationException">ゲームが進行中でない、またはパス不可能な場合</exception>
    public void Pass()
    {
        // ゲーム終了後のパスは不正
        if (!_gameState.IsGameInProgress())
            throw new InvalidOperationException("ゲームが進行中ではありません");

        // 有効手があるのにパスしようとした場合は不正
        if (!OthelloRules.CanPass(_board, _currentPlayer))
            throw new InvalidOperationException("パスはできません");

        _passCount++;
        AdvanceTurn();

        // 両者が連続でパスした場合はゲーム終了
        if (_passCount >= 2)
            EndGame();
    }

    /// <summary>
    /// 直前の着手を 1 手取り消す。
    /// 履歴が初期状態のみの場合（Undo 不可）は false を返す。
    /// </summary>
    /// <returns>Undo が成功したら true、履歴がなければ false</returns>
    public bool Undo()
    {
        // 初期盤面（履歴の 1 件目）よりも戻ることはできない
        if (_history.Count <= 1)
            return false;

        // 最新の履歴を取り除き、その前の盤面に戻す
        _history.RemoveAt(_history.Count - 1);
        _board = _history[^1].Clone();

        // パスカウントをリセットして、Undo 後のパス判定に影響が出ないようにする
        _passCount = 0;

        // ターンを 1 手前のプレイヤーに戻す
        _currentPlayer = _currentPlayer.Opponent();
        if (!_gameState.IsGameOver())
            UpdateGameState();

        return true;
    }

    /// <summary>
    /// 着手またはパスの後にターンを次のプレイヤーへ進める。
    /// 次のプレイヤーに有効手がない場合、自動的にパス（スキップ）する。
    /// 両者ともに有効手がなければゲームを終了する。
    /// </summary>
    private void AdvanceTurn()
    {
        // まず次のプレイヤーへターンを移す
        _currentPlayer = _currentPlayer.Opponent();

        // 次のプレイヤーが着手できない場合を処理する
        if (!HasValidMoves(_currentPlayer))
        {
            if (OthelloRules.CanPass(_board, _currentPlayer))
            {
                // 次のプレイヤーもパス → さらに前のプレイヤーへ戻す
                _currentPlayer = _currentPlayer.Opponent();
                if (!HasValidMoves(_currentPlayer))
                {
                    // 両者ともに有効手なし → ゲーム終了
                    EndGame();
                    return;
                }
            }
        }

        UpdateGameState();
    }

    /// <summary>
    /// 現在のプレイヤー（_currentPlayer）に基づいてゲーム状態を更新する。
    /// ゲームが既に終了している場合は何もしない。
    /// </summary>
    private void UpdateGameState()
    {
        // 終了状態を上書きしないようにガードする
        if (_gameState.IsGameOver())
            return;

        _gameState = _currentPlayer switch
        {
            PlayerColor.Black => GameState.BlackTurn,
            PlayerColor.White => GameState.WhiteTurn,
            // Empty など想定外の値が来た場合は状態を変えない
            _ => _gameState
        };
    }

    /// <summary>
    /// ゲームを終了し、石数に基づいて勝敗を GameState に反映する。
    /// </summary>
    private void EndGame()
    {
        var (winner, _, _) = OthelloRules.GetGameResult(_board);
        _gameState = winner switch
        {
            PlayerColor.Black => GameState.BlackWon,
            PlayerColor.White => GameState.WhiteWon,
            null              => GameState.Draw,    // 同数 → 引き分け
            _                 => GameState.GameOver // 想定外（フォールバック）
        };
    }

    /// <summary>
    /// 指定したプレイヤーに有効手があるかどうかを判定する内部ヘルパー。
    /// </summary>
    /// <param name="playerColor">確認するプレイヤー</param>
    /// <returns>1 手以上の有効手があれば true</returns>
    private bool HasValidMoves(PlayerColor playerColor) =>
        OthelloRules.GetValidMoves(_board, playerColor).Count > 0;

    /// <summary>
    /// 指定したプレイヤーの有効手リストを返す（GameViewModel などの外部から利用）。
    /// </summary>
    /// <param name="playerColor">対象のプレイヤー</param>
    /// <returns>有効な着手位置の Position リスト</returns>
    public List<Position> GetValidMoves(PlayerColor playerColor) =>
        OthelloRules.GetValidMoves(_board, playerColor);

    /// <summary>
    /// 現在の盤面における勝者と双方の石数を返す。
    /// ゲーム終了前でも呼び出せるが、意味を持つのは終局後。
    /// </summary>
    /// <returns>（勝者の色 or null, 黒の石数, 白の石数）のタプル</returns>
    public (PlayerColor? Winner, int BlackScore, int WhiteScore) GetResult() =>
        OthelloRules.GetGameResult(_board);
}
