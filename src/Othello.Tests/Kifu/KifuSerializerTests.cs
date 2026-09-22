namespace Technopro.Othello.Tests.Kifu;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;

/// <summary>
/// KifuSerializer の単体テスト。
/// JSON シリアライズ・デシリアライズの round-trip とエラー処理を検証する。
/// </summary>
public class KifuSerializerTests
{
	/// <summary>テスト用の最小棋譜レコードを生成する（着手なし）。</summary>
	private static KifuRecord MakeEmptyKifu() => new(
		Version: 1,
		PlayedAt: new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.FromHours(9)),
		HumanColor: PlayerColor.Black,
		Difficulty: DifficultyLevel.Medium,
		Result: PlayerColor.Black,
		Moves: [],
		FinalScore: new KifuFinalScore(36, 28));

	// ===== シリアライズ / デシリアライズ =====

	/// <summary>
	/// 着手なし棋譜を JSON に変換して逆変換すると全フィールドが元と一致することを確認する。
	/// パス条件: Deserialize した KifuRecord のすべてのプロパティが元の値と等しいこと。
	/// </summary>
	[Fact]
	public void Serialize_EmptyKifu_RoundTrips()
	{
		var original = MakeEmptyKifu();
		var json = KifuSerializer.Serialize(original);
		var restored = KifuSerializer.Deserialize(json);

		Assert.NotNull(restored);
		Assert.Equal(original.Version, restored.Version);
		Assert.Equal(original.PlayedAt, restored.PlayedAt);
		Assert.Equal(original.HumanColor, restored.HumanColor);
		Assert.Equal(original.Difficulty, restored.Difficulty);
		Assert.Equal(original.Result, restored.Result);
		Assert.Empty(restored.Moves);
		Assert.Equal(original.FinalScore.Black, restored.FinalScore.Black);
		Assert.Equal(original.FinalScore.White, restored.FinalScore.White);
	}

	/// <summary>
	/// 通常着手とパスを含む棋譜が round-trip することを確認する。
	/// パス条件: 手数・各手の Player / Row / Col / IsPass がすべて一致すること。
	/// </summary>
	[Fact]
	public void Serialize_KifuWithPassMove_RoundTrips()
	{
		var moves = new List<KifuMove>
		{
			new(PlayerColor.Black, Row: 2, Col: 3),
			new(PlayerColor.White, Row: 2, Col: 4),
			new(PlayerColor.White, IsPass: true),
		};
		var original = new KifuRecord(1,
			new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.FromHours(9)),
			PlayerColor.Black, DifficultyLevel.Hard, PlayerColor.White,
			moves, new KifuFinalScore(20, 44));

		var json = KifuSerializer.Serialize(original);
		var restored = KifuSerializer.Deserialize(json);

		Assert.NotNull(restored);
		Assert.Equal(3, restored.Moves.Count);
		Assert.Equal(PlayerColor.Black, restored.Moves[0].Player);
		Assert.Equal(2, restored.Moves[0].Row);
		Assert.Equal(3, restored.Moves[0].Col);
		Assert.False(restored.Moves[0].IsPass);
		Assert.True(restored.Moves[2].IsPass);
		Assert.Null(restored.Moves[2].Row);
		Assert.Null(restored.Moves[2].Col);
	}

	/// <summary>
	/// 引き分け（Result = null）が round-trip することを確認する。
	/// パス条件: Deserialize 後も Result が null であること。
	/// </summary>
	[Fact]
	public void Serialize_DrawResult_RoundTrips()
	{
		var original = new KifuRecord(1,
			DateTimeOffset.Now, PlayerColor.White, DifficultyLevel.Easy,
			Result: null, Moves: [], new KifuFinalScore(32, 32));

		var json = KifuSerializer.Serialize(original);
		var restored = KifuSerializer.Deserialize(json);

		Assert.NotNull(restored);
		Assert.Null(restored.Result);
	}

	/// <summary>
	/// 不正な JSON 文字列を渡すと Deserialize が null を返すことを確認する。
	/// パス条件: 戻り値が null であること（例外をスローしないこと）。
	/// </summary>
	[Fact]
	public void Deserialize_InvalidJson_ReturnsNull()
	{
		var result = KifuSerializer.Deserialize("{ not valid json }}");
		Assert.Null(result);
	}

	/// <summary>
	/// 空文字列を渡すと Deserialize が null を返すことを確認する。
	/// パス条件: 戻り値が null であること。
	/// </summary>
	[Fact]
	public void Deserialize_EmptyString_ReturnsNull()
	{
		var result = KifuSerializer.Deserialize(string.Empty);
		Assert.Null(result);
	}

	/// <summary>
	/// 構文的には正しいが必須フィールド（moves/finalScore）が欠落した JSON を渡すと
	/// Deserialize が null を返すことを確認する（Issue #153 回帰）。
	/// パス条件: 戻り値が null であること（後続の KifuPlayer 構築で例外にならないこと）。
	/// </summary>
	[Fact]
	public void Deserialize_MissingRequiredFields_ReturnsNull()
	{
		const string json = """
			{"version":1,"playedAt":"2026-01-01T00:00:00+09:00","humanColor":"black","difficulty":"medium","result":"black"}
			""";

		var result = KifuSerializer.Deserialize(json);

		Assert.Null(result);
	}

	/// <summary>
	/// 非合法な着手（黒の初手に (0,0)）を含む棋譜 JSON を渡すと Deserialize が null を返すことを確認する
	/// （Issue #153 回帰）。
	/// パス条件: 戻り値が null であること（後続の KifuPlayer 構築で例外にならないこと）。
	/// </summary>
	[Fact]
	public void Deserialize_IllegalMove_ReturnsNull()
	{
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Medium,
			Result: null,
			Moves: [new KifuMove(PlayerColor.Black, Row: 0, Col: 0)],
			FinalScore: new KifuFinalScore(4, 0));
		var json = KifuSerializer.Serialize(record);

		var result = KifuSerializer.Deserialize(json);

		Assert.Null(result);
	}

	/// <summary>
	/// moves 配列に null 要素を含む JSON を渡すと Deserialize が null を返すことを確認する
	/// （Issue #190: 手動編集等で moves:[null] のような JSON を渡すと NullReferenceException が
	/// 送出されていた不具合の回帰テスト）。
	/// パス条件: 例外をスローせず戻り値が null であること。
	/// </summary>
	[Fact]
	public void Deserialize_MoveElementIsNull_ReturnsNull()
	{
		const string json = """
			{"version":1,"playedAt":"2026-01-01T00:00:00+09:00","humanColor":"black","difficulty":"medium","result":"black","finalScore":{"black":33,"white":31},"moves":[null]}
			""";

		var result = KifuSerializer.Deserialize(json);

		Assert.Null(result);
	}

	// ===== SaveAsync / LoadAsync（ファイル I/O、Issue #197） =====

	/// <summary>
	/// SaveAsync で書き出したファイルを LoadAsync で読み込むと内容が一致することを確認する。
	/// パス条件: LoadAsync の戻り値が元の KifuRecord と一致すること。
	/// </summary>
	[Fact]
	public async Task SaveAsync_ThenLoadAsync_RoundTrips()
	{
		var original = new KifuRecord(1,
			new DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.FromHours(9)),
			PlayerColor.Black, DifficultyLevel.Hard, PlayerColor.White,
			Moves: [new KifuMove(PlayerColor.Black, Row: 2, Col: 3)],
			FinalScore: new KifuFinalScore(20, 44));

		var filePath = Path.Combine(Path.GetTempPath(), $"kifu_test_{Guid.NewGuid():N}.json");
		try
		{
			await KifuSerializer.SaveAsync(original, filePath);
			var restored = await KifuSerializer.LoadAsync(filePath);

			Assert.NotNull(restored);
			Assert.Equal(original.HumanColor, restored.HumanColor);
			Assert.Equal(original.Difficulty, restored.Difficulty);
			Assert.Equal(original.Result, restored.Result);
			Assert.Single(restored.Moves);
			Assert.Equal(2, restored.Moves[0].Row);
			Assert.Equal(3, restored.Moves[0].Col);
			Assert.Equal(original.FinalScore.Black, restored.FinalScore.Black);
			Assert.Equal(original.FinalScore.White, restored.FinalScore.White);
		}
		finally
		{
			if (File.Exists(filePath))
				File.Delete(filePath);
		}
	}

	/// <summary>
	/// SaveAsync が存在しないディレクトリへの保存でもディレクトリを自動作成することを確認する。
	/// パス条件: 例外をスローせずファイルが作成されること。
	/// </summary>
	[Fact]
	public async Task SaveAsync_CreatesDirectoryIfNotExists()
	{
		var dir = Path.Combine(Path.GetTempPath(), $"kifu_test_dir_{Guid.NewGuid():N}");
		var filePath = Path.Combine(dir, "kifu.json");
		try
		{
			Assert.False(Directory.Exists(dir));

			await KifuSerializer.SaveAsync(MakeEmptyKifu(), filePath);

			Assert.True(File.Exists(filePath));
		}
		finally
		{
			if (Directory.Exists(dir))
				Directory.Delete(dir, recursive: true);
		}
	}

	/// <summary>
	/// 存在しないファイルパスを指定すると LoadAsync が null を返すことを確認する。
	/// パス条件: 戻り値が null であること（例外をスローしないこと）。
	/// </summary>
	[Fact]
	public async Task LoadAsync_FileDoesNotExist_ReturnsNull()
	{
		var filePath = Path.Combine(Path.GetTempPath(), $"kifu_nonexistent_{Guid.NewGuid():N}.json");

		var result = await KifuSerializer.LoadAsync(filePath);

		Assert.Null(result);
	}

	// ===== GetDefaultSaveDirectory（Issue #197） =====

	/// <summary>
	/// GetDefaultSaveDirectory が LocalApplicationData 配下の OthelloCspy\kifu を返すことを確認する。
	/// パス条件: 戻り値に "OthelloCspy" と "kifu" のパス区切りが両方含まれること。
	/// </summary>
	[Fact]
	public void GetDefaultSaveDirectory_ReturnsExpectedPathStructure()
	{
		var result = KifuSerializer.GetDefaultSaveDirectory();

		var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		Assert.StartsWith(expectedBase, result);
		Assert.EndsWith(Path.Combine("OthelloCspy", "kifu"), result);
	}
}
