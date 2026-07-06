namespace Technopro.Othello.Tests.Helpers;

using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

/// <summary><see cref="GameEngine"/> のテストシナリオをセットアップするヘルパーメソッド集。</summary>
public static class TestGameHelper
{
	/// <summary>
	/// 初期化済みの <see cref="GameEngine"/> を作成する。
	/// パス条件: <see cref="GameState.BlackTurn"/> で黒・白それぞれ 2 個が配置された状態が返される。
	/// </summary>
	public static GameEngine CreateInitializedGame()
	{
		var engine = new GameEngine();
		engine.Initialize();
		return engine;
	}

	/// <summary>
	/// 指定した一連の着手を実行した後の <see cref="GameEngine"/> を作成する。
	/// パス条件: すべての着手が成功し、最後の着手後の状態が返される。
	/// </summary>
	/// <exception cref="InvalidOperationException">いずれかの着手が失敗した場合。</exception>
	public static GameEngine CreateGameWithMoves(params Position[] moves)
	{
		var engine = CreateInitializedGame();
		foreach (var move in moves)
		{
			var result = engine.MakeMove(move);
			if (!result.IsSuccess)
				throw new InvalidOperationException(
					$"位置 {move} への着手に失敗しました: {result.Message}");
		}
		return engine;
	}

	/// <summary>
	/// 現在のプレイヤーの有効手一覧を返す。
	/// パス条件: <paramref name="playerColor"/> が打てる <see cref="Position"/> のリストが返される。
	/// </summary>
	public static List<Position> GetValidMoves(GameEngine engine, PlayerColor playerColor)
		=> engine.GetValidMoves(playerColor);

	/// <summary>
	/// ゲームが進行中（<see cref="GameState.BlackTurn"/> または <see cref="GameState.WhiteTurn"/>）かどうかを返す。
	/// パス条件: 進行中なら <c>true</c>、終局状態なら <c>false</c>。
	/// </summary>
	public static bool IsGameActive(GameEngine engine)
		=> engine.GameState.IsGameInProgress();

	/// <summary>
	/// 現在の盤面を返す。
	/// パス条件: 現在のゲーム状態を反映した <see cref="Board"/> が返される。
	/// </summary>
	public static Board GetCurrentBoard(GameEngine engine) => engine.CurrentBoard;

	/// <summary>
	/// 現在の手番プレイヤーを返す。
	/// パス条件: <see cref="PlayerColor.Black"/> または <see cref="PlayerColor.White"/> が返される。
	/// </summary>
	public static PlayerColor GetCurrentPlayer(GameEngine engine) => engine.CurrentPlayer;

	/// <summary>
	/// 現在のゲーム状態を返す。
	/// パス条件: 現在の <see cref="GameState"/> 列挙値が返される。
	/// </summary>
	public static GameState GetGameState(GameEngine engine) => engine.GameState;
}
