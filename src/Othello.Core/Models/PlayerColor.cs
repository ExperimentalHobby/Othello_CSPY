namespace Technopro.Othello.Core.Models;

/// <summary>
/// ボード上の各マスの状態、またはプレイヤーの色を表す列挙型。
/// Empty はマスが空であることを示し、Black / White は石の色に対応する。
/// </summary>
public enum PlayerColor
{
	/// <summary>石が置かれていない空きマス</summary>
	Empty = 0,
	/// <summary>黒プレイヤー（先手）</summary>
	Black = 1,
	/// <summary>白プレイヤー（後手）</summary>
	White = 2
}

/// <summary>
/// PlayerColor 列挙型の拡張メソッドを提供するクラス。
/// </summary>
public static class PlayerColorExtensions
{
	/// <summary>
	/// 指定したプレイヤー色の相手色を返す。
	/// </summary>
	/// <param name="color">基準となるプレイヤー色（Black または White）</param>
	/// <returns>相手の色（Black → White, White → Black）</returns>
	/// <exception cref="ArgumentException">Empty を渡した場合（相手が存在しない）</exception>
	public static PlayerColor Opponent(this PlayerColor color) => color switch
	{
		PlayerColor.Black => PlayerColor.White,
		PlayerColor.White => PlayerColor.Black,
		// Empty はプレイヤーではないため相手が存在しない
		_ => throw new ArgumentException($"Cannot get opponent of {color}")
	};

	/// <summary>
	/// プレイヤー色を日本語の表示文字列に変換する。
	/// </summary>
	/// <param name="color">変換対象の色</param>
	/// <returns>"黒" / "白" / "空" のいずれか</returns>
	public static string ToDisplayString(this PlayerColor color) => color switch
	{
		PlayerColor.Black => "黒",
		PlayerColor.White => "白",
		PlayerColor.Empty => "空",
		_ => "不明"
	};
}
