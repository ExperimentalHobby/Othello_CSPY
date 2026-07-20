namespace Technopro.Othello.Tests.Console;

using Technopro.Othello.Console;
using Technopro.Othello.Core.Models;

public class ConsoleInputParserTests
{
	// ---------- ParseInput 正常系 ----------

	/// <summary>
	/// "d4" 形式（列文字+行番号）を正しく Position(3,3) に変換できることを確認する。
	/// パス条件: Row=3, Column=3 の Position が返ること。
	/// </summary>
	[Fact]
	public void ParseInput_LetterDigitFormat_ReturnsCorrectPosition()
	{
		var result = ConsoleInputParser.ParseInput("d4");
		Assert.NotNull(result);
		Assert.Equal(3, result!.Value.Row);
		Assert.Equal(3, result.Value.Column);
	}

	/// <summary>
	/// "a1" が Position(0,0) に変換されることを確認する（最小値境界）。
	/// パス条件: Row=0, Column=0 の Position が返ること。
	/// </summary>
	[Fact]
	public void ParseInput_A1Format_ReturnsOrigin()
	{
		var result = ConsoleInputParser.ParseInput("a1");
		Assert.NotNull(result);
		Assert.Equal(0, result!.Value.Row);
		Assert.Equal(0, result.Value.Column);
	}

	/// <summary>
	/// "h8" が Position(7,7) に変換されることを確認する（最大値境界）。
	/// パス条件: Row=7, Column=7 の Position が返ること。
	/// </summary>
	[Fact]
	public void ParseInput_H8Format_ReturnsMaxPosition()
	{
		var result = ConsoleInputParser.ParseInput("h8");
		Assert.NotNull(result);
		Assert.Equal(7, result!.Value.Row);
		Assert.Equal(7, result.Value.Column);
	}

	/// <summary>
	/// "3 4" 形式（スペース区切りの行・列インデックス）を正しく変換できることを確認する。
	/// パス条件: Row=3, Column=4 の Position が返ること。
	/// </summary>
	[Fact]
	public void ParseInput_SpaceDelimitedFormat_ReturnsCorrectPosition()
	{
		var result = ConsoleInputParser.ParseInput("3 4");
		Assert.NotNull(result);
		Assert.Equal(3, result!.Value.Row);
		Assert.Equal(4, result.Value.Column);
	}

	/// <summary>
	/// "0 0" 形式が Position(0,0) に変換されることを確認する（スペース区切り最小値境界）。
	/// パス条件: Row=0, Column=0 の Position が返ること。
	/// </summary>
	[Fact]
	public void ParseInput_SpaceDelimitedZeroZero_ReturnsOrigin()
	{
		var result = ConsoleInputParser.ParseInput("0 0");
		Assert.NotNull(result);
		Assert.Equal(0, result!.Value.Row);
		Assert.Equal(0, result.Value.Column);
	}

	// ---------- ParseInput 異常系 ----------

	/// <summary>
	/// 空文字列は null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_EmptyString_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput(""));
	}

	/// <summary>
	/// 範囲外の文字列 "z9" は null を返すことを確認する（列が 'h' を超える）。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_OutOfRangeLetterDigit_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("z9"));
	}

	/// <summary>
	/// "i9" は Position.IsValid で false になるため null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_ColumnBeyondH_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("i9"));
	}

	/// <summary>
	/// "8 8" は Position の範囲外（0-7）のため null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_SpaceDelimitedOutOfRange_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("8 8"));
	}

	/// <summary>
	/// "abc" など認識できない形式は null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_UnrecognizedFormat_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("abc"));
	}

	/// <summary>
	/// "undo" などコマンド文字列は Position ではなく null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_UndoString_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("undo"));
	}

	/// <summary>
	/// null を渡すと例外を catch して null を返すことを確認する（catch ブロックのカバー）。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_Null_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput(null!));
	}

	/// <summary>
	/// 大文字の列文字 "D4" は 'd' - 'a' 換算外となるため null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void ParseInput_UpperCaseFormat_ReturnsNull()
	{
		Assert.Null(ConsoleInputParser.ParseInput("D4"));
	}
}
