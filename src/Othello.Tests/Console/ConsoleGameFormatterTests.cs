namespace Technopro.Othello.Tests.Console;

using Technopro.Othello.Console;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

/// <summary>
/// ConsoleGameFormatter の単体テスト。
/// Program.cs から切り出した、I/O に依存しない純粋ロジック（入力解釈・表示文字列生成）を検証する（Issue #193）。
/// </summary>
public class ConsoleGameFormatterTests
{
	// ---------- ParseDifficultyChoice ----------

	/// <summary>
	/// "0"〜"4" がそれぞれ対応する DifficultyLevel に変換されることを確認する。
	/// パス条件: 各入力に対応する DifficultyLevel が返ること。
	/// </summary>
	[Theory]
	[InlineData("0", DifficultyLevel.Beginner)]
	[InlineData("1", DifficultyLevel.Easy)]
	[InlineData("2", DifficultyLevel.Medium)]
	[InlineData("3", DifficultyLevel.Hard)]
	[InlineData("4", DifficultyLevel.Expert)]
	public void ParseDifficultyChoice_ValidInput_ReturnsMappedDifficulty(string input, DifficultyLevel expected)
	{
		Assert.Equal(expected, ConsoleGameFormatter.ParseDifficultyChoice(input));
	}

	/// <summary>
	/// null・空文字・不正な文字列は既定値（Medium）になることを確認する。
	/// パス条件: いずれも DifficultyLevel.Medium が返ること。
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("abc")]
	[InlineData("9")]
	public void ParseDifficultyChoice_InvalidInput_ReturnsMedium(string? input)
	{
		Assert.Equal(DifficultyLevel.Medium, ConsoleGameFormatter.ParseDifficultyChoice(input));
	}

	/// <summary>
	/// 前後の空白は Trim されてから判定されることを確認する。
	/// パス条件: " 3 " が Hard として扱われること。
	/// </summary>
	[Fact]
	public void ParseDifficultyChoice_TrimsWhitespace()
	{
		Assert.Equal(DifficultyLevel.Hard, ConsoleGameFormatter.ParseDifficultyChoice(" 3 "));
	}

	// ---------- ParseColorChoice ----------

	/// <summary>
	/// "1" は Black、"2" は White に変換されることを確認する。
	/// パス条件: 各入力に対応する PlayerColor が返ること。
	/// </summary>
	[Theory]
	[InlineData("1", PlayerColor.Black)]
	[InlineData("2", PlayerColor.White)]
	public void ParseColorChoice_ValidInput_ReturnsMappedColor(string input, PlayerColor expected)
	{
		Assert.Equal(expected, ConsoleGameFormatter.ParseColorChoice(input));
	}

	/// <summary>
	/// null・空文字・不正な文字列は既定値（Black）になることを確認する。
	/// パス条件: いずれも PlayerColor.Black が返ること。
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("3")]
	public void ParseColorChoice_InvalidInput_ReturnsBlack(string? input)
	{
		Assert.Equal(PlayerColor.Black, ConsoleGameFormatter.ParseColorChoice(input));
	}

	// ---------- FormatBoard ----------

	/// <summary>
	/// 初期盤面の文字列表現に列ヘッダー・中央4石・スコア行が含まれることを確認する。
	/// パス条件: ヘッダー行・黒白の石記号・スコア表示がすべて出力に含まれること。
	/// </summary>
	[Fact]
	public void FormatBoard_InitialBoard_ContainsHeaderStonesAndScore()
	{
		var board = new Board();

		var result = ConsoleGameFormatter.FormatBoard(board, blackScore: 2, whiteScore: 2);

		Assert.Contains("a  b  c  d  e  f  g  h", result);
		Assert.Contains("●", result); // 黒石
		Assert.Contains("○", result); // 白石
		Assert.Contains("黒: 2  白: 2", result);
	}

	/// <summary>
	/// 盤面が 8 行分（1〜8）出力されることを確認する。
	/// パス条件: "1 |" から "8 |" まですべて出力に含まれること。
	/// </summary>
	[Fact]
	public void FormatBoard_ContainsAllEightRows()
	{
		var board = new Board();
		var result = ConsoleGameFormatter.FormatBoard(board, blackScore: 2, whiteScore: 2);

		for (int row = 1; row <= 8; row++)
			Assert.Contains($"{row} |", result);
	}

	// ---------- FormatPassNotice ----------

	/// <summary>
	/// パスが発生していない場合（null）は null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void FormatPassNotice_NoPass_ReturnsNull()
	{
		Assert.Null(ConsoleGameFormatter.FormatPassNotice(null));
	}

	/// <summary>
	/// パスが発生した場合、対象プレイヤーを含む通知文言を返すことを確認する。
	/// パス条件: 戻り値に "黒" とパス文言が含まれること。
	/// </summary>
	[Fact]
	public void FormatPassNotice_PlayerPassed_ReturnsMessageWithPlayerName()
	{
		var message = ConsoleGameFormatter.FormatPassNotice(PlayerColor.Black);

		Assert.NotNull(message);
		Assert.Contains("黒", message);
		Assert.Contains("パスしました", message);
	}

	// ---------- FormatResultMessage ----------

	/// <summary>
	/// 引き分け（winner = null）の場合は「引き分け!」を返すことを確認する。
	/// </summary>
	[Fact]
	public void FormatResultMessage_Draw_ReturnsDrawMessage()
	{
		Assert.Equal("引き分け!", ConsoleGameFormatter.FormatResultMessage(null, PlayerColor.Black));
	}

	/// <summary>
	/// 勝者が人間の色と一致する場合は「あなたの勝利!」を返すことを確認する。
	/// </summary>
	[Fact]
	public void FormatResultMessage_HumanWins_ReturnsHumanWinMessage()
	{
		Assert.Equal("あなたの勝利!", ConsoleGameFormatter.FormatResultMessage(PlayerColor.Black, PlayerColor.Black));
	}

	/// <summary>
	/// 勝者が人間の色と異なる場合は「AI の勝利!」を返すことを確認する。
	/// </summary>
	[Fact]
	public void FormatResultMessage_AiWins_ReturnsAiWinMessage()
	{
		Assert.Equal("AI の勝利!", ConsoleGameFormatter.FormatResultMessage(PlayerColor.White, PlayerColor.Black));
	}
}
