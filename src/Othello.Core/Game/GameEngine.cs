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
	/// 直前のターン遷移で「有効手がなく強制パスになった」プレイヤー。
	/// パスがなかった場合は null。UI/コンソールでのパス通知に使用する（Undo / Initialize でリセット）。
	/// </summary>
	private PlayerColor? _lastPassedPlayer;

	/// <summary>
	/// Undo 用の状態スナップショット履歴。
	/// 各着手後に「盤面・手番・ゲーム状態」を一括で積む。Initialize 時の初期状態も含む。
	/// 手番を単純反転するのではなくスナップショットを直接復元することで、
	/// パスをまたぐ Undo でも手番・状態が正しく戻る。
	/// </summary>
	private readonly List<Snapshot> _history = new();

	/// <summary>
	/// Undo 用に保存する 1 時点の完全な状態（盤面・手番・ゲーム状態）。
	/// 盤面は復元のたびに Clone するため、履歴内のスナップショットは不変に保たれる。
	/// </summary>
	private readonly record struct Snapshot(Board Board, PlayerColor Player, GameState State);


	/// <summary>現在の盤面（読み取り専用）</summary>
	public Board CurrentBoard => _board;

	/// <summary>まだ一手も打たれていない初期盤面状態か（設定変更可否の判定に使用）</summary>
	public bool IsInitialState => _history.Count <= 1;

	/// <summary>現在のゲーム進行状態</summary>
	public GameState GameState => _gameState;

	/// <summary>現在着手すべきプレイヤーの色</summary>
	public PlayerColor CurrentPlayer => _currentPlayer;

	/// <summary>
	/// 直前のターン遷移で強制パスになったプレイヤー（パスがなければ null）。
	/// 自動スキップされたパスを UI 側で通知するために参照する。
	/// </summary>
	public PlayerColor? LastPassedPlayer => _lastPassedPlayer;

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
		_lastPassedPlayer = null;
		_history.Clear();
		// 初期状態を履歴の先頭として保存する（Undo の下限）
		PushSnapshot();
	}

	/// <summary>
	/// 現在の状態（盤面・手番・ゲーム状態）をスナップショットとして履歴に積む。
	/// 盤面は Clone して保存するため、以後の盤面変更が履歴に波及しない。
	/// 常に履歴の末尾＝現在の状態となる不変条件を維持する。
	/// </summary>
	private void PushSnapshot() => _history.Add(new Snapshot(_board.Clone(), _currentPlayer, _gameState));

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

		// 手番・状態を進めてから、その結果を 1 つのスナップショットとして履歴に積む
		// （履歴の末尾＝現在の状態という不変条件を保つ）
		AdvanceTurn();
		PushSnapshot();
		return MoveResult.Success("移動に成功しました", flipped);
	}

	/// <summary>
	/// 現在のプレイヤーがパスする（有効手がない場合のみ呼び出し可能）。
	/// 両者ともに有効手がない場合は AdvanceTurn 内でゲームを終了する。
	/// UI/コンソールは AdvanceTurn による自動スキップを利用するため、このメソッドを直接呼ばない。
	/// テスト・将来の拡張のために残す。
	/// </summary>
	/// <exception cref="InvalidOperationException">ゲームが進行中でない、またはパス不可能な場合</exception>
	internal void Pass()
	{
		// ゲーム終了後のパスは不正
		if (!_gameState.IsGameInProgress())
			throw new InvalidOperationException("ゲームが進行中ではありません");

		// 有効手があるのにパスしようとした場合は不正
		if (!OthelloRules.CanPass(_board, _currentPlayer))
			throw new InvalidOperationException("パスはできません");

		AdvanceTurn();
		// 履歴の末尾＝現在の状態という不変条件を保つため、パス後もスナップショットを積む
		PushSnapshot();
	}

	/// <summary>
	/// 直前の着手を 1 手取り消す。
	/// 履歴が初期状態のみの場合（Undo 不可）は false を返す。
	/// </summary>
	/// <returns>Undo が成功したら true、履歴がなければ false</returns>
	public bool Undo()
	{
		// 初期状態（履歴の 1 件目）よりも戻ることはできない
		if (_history.Count <= 1)
			return false;

		// 最新のスナップショットを取り除き、その前の状態を丸ごと復元する。
		// 手番・ゲーム状態もスナップショットから直接戻すため、
		// パスをまたぐ Undo や終局後の Undo でも整合性が保たれる。
		_history.RemoveAt(_history.Count - 1);
		var prev = _history[^1];
		_board = prev.Board.Clone();
		_currentPlayer = prev.Player;
		_gameState = prev.State;
		_lastPassedPlayer = null;

		return true;
	}

	/// <summary>
	/// 着手またはパスの後にターンを次のプレイヤーへ進める。
	/// 次のプレイヤーに有効手がない場合、自動的にスキップして元のプレイヤーへ戻す。
	/// 両者ともに有効手がなければゲームを終了する。
	/// </summary>
	private void AdvanceTurn()
	{
		// 今回の遷移でのパス記録をリセットしてから判定する
		_lastPassedPlayer = null;

		// まず次のプレイヤーへターンを移す
		_currentPlayer = _currentPlayer.Opponent();

		// 次のプレイヤーが着手できない場合を処理する
		if (!HasValidMoves(_currentPlayer))
		{
			// 次のプレイヤーは有効手なし → 強制パス（通知用に記録）
			_lastPassedPlayer = _currentPlayer;
			_currentPlayer = _currentPlayer.Opponent();
			if (!HasValidMoves(_currentPlayer))
			{
				// 両者ともに有効手なし → ゲーム終了
				EndGame();
				return;
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
			// AdvanceTurn は常に Black/White のみを _currentPlayer にセットするため実際には到達しない。
			// C# のパターンマッチング網羅性のために記載する。
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
			null => GameState.Draw,    // 同数 → 引き分け
									   // GetGameResult は Black/White/null のみを返すため、このケースは実際には到達しない。
									   // C# のパターンマッチング網羅性のために記載する。
			_ => GameState.GameOver
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

	/// <summary>
	/// テスト専用: 任意の盤面・手番からゲームを進行状態にセットアップする。
	/// 強制パス・終局・パスをまたぐ Undo などの境界シナリオを再現するために使用する。
	/// 与えた currentPlayer に有効手があることは呼び出し側が保証すること。
	/// </summary>
	/// <param name="board">開始盤面（Clone して取り込む）</param>
	/// <param name="currentPlayer">手番のプレイヤー（Black または White）</param>
	internal void LoadStateForTest(Board board, PlayerColor currentPlayer)
	{
		_board = board.Clone();
		_currentPlayer = currentPlayer;
		_lastPassedPlayer = null;
		_gameState = currentPlayer == PlayerColor.White ? GameState.WhiteTurn : GameState.BlackTurn;
		_history.Clear();
		PushSnapshot();
	}
}
