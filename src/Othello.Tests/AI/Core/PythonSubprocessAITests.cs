namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

public class SerializeBoardTests
{
	/// <summary>
	/// 初期盤面をシリアライズすると 8×8 の配列が返ることを確認する。
	/// パス条件: grid.Length が 8、各行の長さが 8 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_InitialBoard_Returns8x8Grid()
	{
		var board = new Board();
		var grid = PythonSubprocessAI.SerializeBoard(board);

		Assert.Equal(8, grid.Length);
		foreach (var row in grid)
			Assert.Equal(8, row.Length);
	}

	/// <summary>
	/// Empty マスのシリアライズ値が 0 であることを確認する。
	/// 初期盤面の (0,0) は Empty → grid[0][0] = 0。
	/// パス条件: grid[0][0] が 0 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_EmptySquare_ReturnsZero()
	{
		var board = new Board();
		var grid = PythonSubprocessAI.SerializeBoard(board);

		Assert.Equal(0, grid[0][0]);
	}

	/// <summary>
	/// Black マスのシリアライズ値が 1 であることを確認する。
	/// 初期盤面で (3,4) は Black → grid[3][4] = 1。
	/// パス条件: grid[3][4] が 1 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_BlackSquare_ReturnsOne()
	{
		var board = new Board();
		var grid = PythonSubprocessAI.SerializeBoard(board);

		// 初期配置: (3,4) = Black
		Assert.Equal(1, grid[3][4]);
	}

	/// <summary>
	/// White マスのシリアライズ値が 2 であることを確認する。
	/// 初期盤面で (3,3) は White → grid[3][3] = 2。
	/// パス条件: grid[3][3] が 2 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_WhiteSquare_ReturnsTwo()
	{
		var board = new Board();
		var grid = PythonSubprocessAI.SerializeBoard(board);

		// 初期配置: (3,3) = White
		Assert.Equal(2, grid[3][3]);
	}

	/// <summary>
	/// 任意に石を置いた盤面のシリアライズ値が正しいことを確認する。
	/// (0,0) を Black にセットすると grid[0][0] = 1 になること。
	/// パス条件: grid[0][0] が 1 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_ManuallySetBlack_ReturnsOne()
	{
		var board = new Board();
		board.SetPiece(0, 0, PlayerColor.Black);
		var grid = PythonSubprocessAI.SerializeBoard(board);

		Assert.Equal(1, grid[0][0]);
	}

	/// <summary>
	/// 全マスを White にした盤面で、すべてのセルが 2 になることを確認する。
	/// パス条件: grid の全要素が 2 であること。
	/// </summary>
	[Fact]
	public void SerializeBoard_AllWhite_AllCellsReturnTwo()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.White);

		var grid = PythonSubprocessAI.SerializeBoard(board);

		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				Assert.Equal(2, grid[r][c]);
	}
}

/// <summary>
/// PythonSubprocessAI.ParsePythonMajorVersion の単体テスト。
/// python --version の出力文字列からメジャーバージョン番号を取り出す内部ロジックを検証する。
/// </summary>
public class ParsePythonMajorVersionTests
{
	/// <summary>
	/// "Python 3.11.2" のような標準出力から 3 が得られること。
	/// パス条件: 戻り値が 3 であること。
	/// </summary>
	[Fact]
	public void ParsePythonMajorVersion_Python3_ReturnsThree()
	{
		int version = PythonSubprocessAI.ParsePythonMajorVersion("Python 3.11.2");
		Assert.Equal(3, version);
	}

	/// <summary>
	/// "Python 2.7.18" のような出力から 2 が得られること。
	/// パス条件: 戻り値が 2 であること。
	/// </summary>
	[Fact]
	public void ParsePythonMajorVersion_Python2_ReturnsTwo()
	{
		int version = PythonSubprocessAI.ParsePythonMajorVersion("Python 2.7.18");
		Assert.Equal(2, version);
	}

	/// <summary>
	/// 空文字列や "Python" プレフィックスのない文字列では -1 が返ること。
	/// パス条件: 戻り値が -1 であること。
	/// </summary>
	[Theory]
	[InlineData("")]
	[InlineData("3.11.2")]
	[InlineData("not python output")]
	public void ParsePythonMajorVersion_InvalidInput_ReturnsMinusOne(string input)
	{
		int version = PythonSubprocessAI.ParsePythonMajorVersion(input);
		Assert.Equal(-1, version);
	}

	/// <summary>
	/// "Python 3" のようにドットなしの文字列からメジャーバージョン 3 が得られること。
	/// パス条件: 戻り値が 3 であること。
	/// </summary>
	[Fact]
	public void ParsePythonMajorVersion_NoDot_ReturnsMajorVersion()
	{
		int version = PythonSubprocessAI.ParsePythonMajorVersion("Python 3");
		Assert.Equal(3, version);
	}

	/// <summary>
	/// "Python X.Y.Z" のようにメジャー部分が非数値の場合は -1 が返ること。
	/// パス条件: 戻り値が -1 であること。
	/// </summary>
	[Fact]
	public void ParsePythonMajorVersion_NonNumericMajor_ReturnsMinusOne()
	{
		int version = PythonSubprocessAI.ParsePythonMajorVersion("Python X.Y.Z");
		Assert.Equal(-1, version);
	}
}

/// <summary>
/// AiScriptPaths の回帰テスト。
/// </summary>
public class AiScriptPathsTests
{
	/// <summary>
	/// IsRustAvailable は Othello.Python ディレクトリが存在しない場合でも例外を投げず false を返す。
	/// 修正前は Directory.EnumerateFiles が DirectoryNotFoundException を投げる恐れがあった。
	/// パス条件: プロパティへのアクセスで例外が発生しないこと（テスト環境では通常 false）。
	/// </summary>
	[Fact]
	public void IsRustAvailable_WhenDirectoryMayNotExist_DoesNotThrow()
	{
		var ex = Record.Exception(() => _ = AiScriptPaths.IsRustAvailable);
		Assert.Null(ex);
	}
}

/// <summary>
/// PythonSubprocessAI コンストラクタの異常系テスト。
/// </summary>
public class PythonSubprocessAIConstructorTests
{
	/// <summary>
	/// 存在しないスクリプトパスを渡すと FileNotFoundException がスローされる。
	/// パス条件: FileNotFoundException がスローされること。
	/// </summary>
	[Fact]
	public void Constructor_NonExistentScript_ThrowsFileNotFoundException()
	{
		Assert.Throws<FileNotFoundException>(() =>
			new PythonSubprocessAI(DifficultyLevel.Easy, "/nonexistent/path/ai.py"));
	}
}

/// <summary>
/// PythonSubprocessAI のバックエンドハンドシェイク統合テスト。
/// ai.py が起動直後に backend フィールドを含む JSON を出力し、
/// PythonSubprocessAI.EngineName がその値と一致することを確認する。
/// Python スクリプトが出力ディレクトリに存在しない場合はスキップする。
/// </summary>
public class PythonSubprocessAIHandshakeTests
{
	private static readonly string ScriptPath = AiScriptPaths.AiScriptPath;

	/// <summary>
	/// PythonSubprocessAI を起動したとき、EngineName が "AI: Rust" または "AI: Python" の
	/// いずれかであることを確認する（ハンドシェイクの結果が反映されていること）。
	/// パス条件: EngineName が期待する 2 値のどちらかと一致すること。
	/// </summary>
	[Fact]
	public void EngineName_MatchesHandshakeBackend()
	{
		if (!File.Exists(ScriptPath))
		{
			// テスト環境に Python スクリプトがない場合はスキップ
			return;
		}

		using var ai = new PythonSubprocessAI(DifficultyLevel.Easy, ScriptPath);

		Assert.True(
			ai.EngineName == "AI: Rust" || ai.EngineName == "AI: Python",
			$"予期しない EngineName: {ai.EngineName}");
	}
}

/// <summary>
/// PythonSubprocessAI.ResolveExecutableOnPath の単体テスト（Issue #229）。
/// 実行ファイル名を PATH 上の絶対パスへ解決し、カレントディレクトリ等の相対位置を拾わないことを検証する。
/// </summary>
public sealed class ResolveExecutableOnPathTests : IDisposable
{
	private readonly string _dirA = Path.Combine(Path.GetTempPath(), $"pathtest_{Guid.NewGuid():N}_a");
	private readonly string _dirB = Path.Combine(Path.GetTempPath(), $"pathtest_{Guid.NewGuid():N}_b");
	private static string FileNameOf(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

	public ResolveExecutableOnPathTests()
	{
		Directory.CreateDirectory(_dirA);
		Directory.CreateDirectory(_dirB);
	}

	public void Dispose()
	{
		Directory.Delete(_dirA, recursive: true);
		Directory.Delete(_dirB, recursive: true);
	}

	private static string Join(params string[] dirs) => string.Join(Path.PathSeparator, dirs);

	/// <summary>
	/// PATH 上のディレクトリにある実行ファイルのフルパスが返ることを確認する。
	/// パス条件: 2 番目のディレクトリにだけあるファイルが、そのディレクトリの絶対パスで返ること。
	/// </summary>
	[Fact]
	public void ResolveExecutableOnPath_FindsFileInPathDirectory_ReturnsFullPath()
	{
		var expected = Path.Combine(_dirB, FileNameOf("fakepy"));
		File.WriteAllText(expected, "");

		var result = PythonSubprocessAI.ResolveExecutableOnPath("fakepy", Join(_dirA, _dirB));

		Assert.Equal(expected, result);
	}

	/// <summary>
	/// 複数ディレクトリに存在する場合は PATH の先頭側が優先されることを確認する。
	/// パス条件: _dirA と _dirB の両方にあるとき _dirA 側のパスが返ること。
	/// </summary>
	[Fact]
	public void ResolveExecutableOnPath_MultipleMatches_ReturnsFirstInPathOrder()
	{
		File.WriteAllText(Path.Combine(_dirA, FileNameOf("fakepy")), "");
		File.WriteAllText(Path.Combine(_dirB, FileNameOf("fakepy")), "");

		var result = PythonSubprocessAI.ResolveExecutableOnPath("fakepy", Join(_dirB, _dirA));

		Assert.Equal(Path.Combine(_dirB, FileNameOf("fakepy")), result);
	}

	/// <summary>
	/// PATH 上に存在しない場合は null を返すことを確認する。
	/// パス条件: どのディレクトリにも無いファイル名で null が返ること。
	/// </summary>
	[Fact]
	public void ResolveExecutableOnPath_NotFound_ReturnsNull()
	{
		Assert.Null(PythonSubprocessAI.ResolveExecutableOnPath("fakepy", Join(_dirA, _dirB)));
	}

	/// <summary>
	/// 相対パスの PATH 要素（カレントディレクトリ経由の乗っ取り経路）と空要素を無視することを確認する。
	/// パス条件: PATH が相対要素・空要素のみのとき、カレントディレクトリにファイルがあっても null が返ること。
	/// </summary>
	[Fact]
	public void ResolveExecutableOnPath_RelativeAndEmptyEntries_AreIgnored()
	{
		var cwdFile = Path.Combine(Directory.GetCurrentDirectory(), FileNameOf("fakepy_cwd"));
		File.WriteAllText(cwdFile, "");
		try
		{
			Assert.Null(PythonSubprocessAI.ResolveExecutableOnPath("fakepy_cwd", Join(".", "", "relative")));
		}
		finally
		{
			File.Delete(cwdFile);
		}
	}

	/// <summary>
	/// PATH が null や空文字列でも例外を投げず null を返すことを確認する。
	/// パス条件: null / 空文字列のどちらでも null が返ること。
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void ResolveExecutableOnPath_NullOrEmptyPath_ReturnsNull(string? pathVar)
	{
		Assert.Null(PythonSubprocessAI.ResolveExecutableOnPath("fakepy", pathVar));
	}
}
