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
}
