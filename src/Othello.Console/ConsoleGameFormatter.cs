namespace Technopro.Othello.Console;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

/// <summary>
/// コンソール版の対話ループ（Program.cs）から、I/O に依存しない純粋ロジックを切り出したクラス。
/// 入力文字列の解釈・表示文字列の生成のみを担当し、Console.ReadLine()/Console.Write() は呼ばない。
/// ConsoleInputParser と同じ狙い（テスト容易性の確保）で Issue #193 にて追加した。
/// </summary>
internal static class ConsoleGameFormatter
{
	/// <summary>
	/// 難易度選択の入力文字列を DifficultyLevel に変換する。
	/// 前後の空白は無視する。"2" または不正な入力はすべて既定値（Medium）として扱う。
	/// </summary>
	/// <param name="rawInput">Console.ReadLine() で得られた生の入力文字列（null 可）</param>
	/// <returns>対応する DifficultyLevel。不正入力の場合は DifficultyLevel.Medium</returns>
	public static DifficultyLevel ParseDifficultyChoice(string? rawInput) =>
		rawInput?.Trim() switch
		{
			"0" => DifficultyLevel.Beginner,
			"1" => DifficultyLevel.Easy,
			"3" => DifficultyLevel.Hard,
			"4" => DifficultyLevel.Expert,
			_ => DifficultyLevel.Medium  // 2 または不正入力はノーマルに統一する
		};

	/// <summary>
	/// 人間の色選択の入力文字列を PlayerColor に変換する。
	/// 前後の空白は無視する。"1" または不正な入力はすべて既定値（Black）として扱う。
	/// </summary>
	/// <param name="rawInput">Console.ReadLine() で得られた生の入力文字列（null 可）</param>
	/// <returns>対応する PlayerColor。不正入力の場合は PlayerColor.Black</returns>
	public static PlayerColor ParseColorChoice(string? rawInput) =>
		rawInput?.Trim() switch
		{
			"2" => PlayerColor.White,
			_ => PlayerColor.Black  // 1 または不正入力は黒に統一する
		};

	/// <summary>
	/// 盤面をテキスト形式の文字列に変換する。
	/// 黒=●、白=○、空=· で表示し、最下行にスコアを表示する。
	/// 各行は改行コードで終端される（末尾にも改行を含む）。
	/// </summary>
	/// <param name="board">表示する盤面</param>
	/// <param name="blackScore">黒の現在の石数</param>
	/// <param name="whiteScore">白の現在の石数</param>
	/// <returns>盤面の文字列表現</returns>
	public static string FormatBoard(Board board, int blackScore, int whiteScore)
	{
		var sb = new System.Text.StringBuilder();

		// 列ヘッダー（a〜h）を出力する
		sb.AppendLine("    a  b  c  d  e  f  g  h");
		sb.AppendLine("  +------------------------+");

		for (int r = 0; r < 8; r++)
		{
			sb.Append($"{r + 1} |"); // 行番号（1〜8）を左端に表示する
			for (int c = 0; c < 8; c++)
			{
				// 石の種類に応じて記号を切り替える
				string sym = board.GetPiece(r, c) switch
				{
					PlayerColor.Black => " ●",
					PlayerColor.White => " ○",
					_ => " ·" // Empty
				};
				sb.Append(sym);
			}
			sb.AppendLine(" |");
		}

		sb.AppendLine("  +------------------------+");
		sb.AppendLine($"  黒: {blackScore}  白: {whiteScore}");

		return sb.ToString();
	}

	/// <summary>
	/// 直前のターン遷移で強制パスが発生していた場合の通知文言を生成する。
	/// </summary>
	/// <param name="lastPassedPlayer">GameEngine.LastPassedPlayer（パスがなければ null）</param>
	/// <returns>パスがあれば通知文言、なければ null</returns>
	public static string? FormatPassNotice(PlayerColor? lastPassedPlayer) =>
		lastPassedPlayer is { } passed
			? $"{passed.ToDisplayString()} は打てる場所がないためパスしました"
			: null;

	/// <summary>
	/// 終局時の勝敗表示文言を生成する。
	/// </summary>
	/// <param name="winner">勝者の色（引き分けの場合は null）</param>
	/// <param name="humanColor">人間が担当していた色</param>
	/// <returns>"引き分け!" / "あなたの勝利!" / "AI の勝利!" のいずれか</returns>
	public static string FormatResultMessage(PlayerColor? winner, PlayerColor humanColor)
	{
		if (winner == null)
			return "引き分け!";
		return winner == humanColor ? "あなたの勝利!" : "AI の勝利!";
	}
}
