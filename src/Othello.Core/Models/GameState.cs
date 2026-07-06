namespace Technopro.Othello.Core.Models;

/// <summary>
/// ゲームの進行状態を表す列挙型。
/// GameEngine が内部で保持し、ターン進行・終了判定・UI 表示の基準となる。
/// </summary>
public enum GameState
{
	/// <summary>ゲーム開始前または初期化中</summary>
	Initialize = 0,
	/// <summary>黒のターン（ゲーム進行中）</summary>
	BlackTurn = 1,
	/// <summary>白のターン（ゲーム進行中）</summary>
	WhiteTurn = 2,
	/// <summary>何らかの理由でゲームが終了した状態（勝敗未確定）</summary>
	GameOver = 3,
	/// <summary>黒が勝利してゲーム終了</summary>
	BlackWon = 4,
	/// <summary>白が勝利してゲーム終了</summary>
	WhiteWon = 5,
	/// <summary>引き分けでゲーム終了</summary>
	Draw = 6
}

/// <summary>
/// GameState 列挙型の拡張メソッドを提供するクラス。
/// </summary>
public static class GameStateExtensions
{
	/// <summary>
	/// ゲームが現在進行中（いずれかのプレイヤーのターン）かどうかを判定する。
	/// UI の入力受付可否や AI 呼び出しの前提条件チェックに使用する。
	/// </summary>
	/// <param name="state">判定対象の GameState</param>
	/// <returns>BlackTurn または WhiteTurn の場合 true</returns>
	public static bool IsGameInProgress(this GameState state) =>
		state == GameState.BlackTurn || state == GameState.WhiteTurn;

	/// <summary>
	/// ゲームが終了済みかどうかを判定する。
	/// </summary>
	/// <param name="state">判定対象の GameState</param>
	/// <returns>GameOver / BlackWon / WhiteWon / Draw のいずれかであれば true</returns>
	public static bool IsGameOver(this GameState state) =>
		state == GameState.GameOver || state == GameState.BlackWon ||
		state == GameState.WhiteWon || state == GameState.Draw;

	/// <summary>
	/// ゲーム状態を日本語の表示文字列に変換する。
	/// </summary>
	/// <param name="state">変換対象の状態</param>
	/// <returns>状態に対応する日本語文字列</returns>
	public static string ToDisplayString(this GameState state) => state switch
	{
		GameState.Initialize => "初期化中",
		GameState.BlackTurn => "黒のターン",
		GameState.WhiteTurn => "白のターン",
		GameState.GameOver => "ゲーム終了",
		GameState.BlackWon => "黒が勝ちました",
		GameState.WhiteWon => "白が勝ちました",
		GameState.Draw => "引き分け",
		_ => "不明な状態"
	};
}
