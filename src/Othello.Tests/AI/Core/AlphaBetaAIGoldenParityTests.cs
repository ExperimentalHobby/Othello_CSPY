namespace Technopro.Othello.Tests.AI.Core;

using System.Text.Json;
using System.Text.Json.Serialization;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

/// <summary>
/// C# AI（AlphaBetaAI）と Python/Rust AI の着手一致を検証する golden テスト（Issue #162）。
///
/// test_parity.py は Rust ↔ Python の一致のみを検証しており、ヒント機能・CPU vs CPU・
/// Python 起動失敗時のフォールバックで実際に使用される C# AI は対象外だった。
///
/// tt_golden_master.json（Othello.AI/Python/test_data 由来、ビルド時に本プロジェクトへコピー）は
/// TT 導入前の alpha_beta_py.AlphaBetaAI で自己対局を行い採取した
/// (board, player, depth[, time_ms]) → 着手 のスナップショットで、Python 側の
/// TranspositionTableEquivalenceTests でも「TT 導入前後で着手選択が変わらないこと」の
/// 参照データとして使われている。この同じデータを C# AlphaBetaAI でも検証することで、
/// 3 実装の着手一致を推移的に確認できる（time_ms を伴う反復深化レコードはタイミング依存で
/// 環境間の再現性がないため対象外とし、固定深さのレコードのみを対象とする）。
/// </summary>
public class AlphaBetaAIGoldenParityTests
{
	private record GoldenRecord(
		int[][] Board,
		int Player,
		int Depth,
		int[]? Move,
		[property: JsonPropertyName("time_ms")] int? TimeMs);

	private static List<GoldenRecord> LoadGoldenRecords()
	{
		string path = Path.Combine(AppContext.BaseDirectory, "test_data", "tt_golden_master.json");
		string json = File.ReadAllText(path);
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		return JsonSerializer.Deserialize<List<GoldenRecord>>(json, options)
			?? throw new InvalidOperationException($"golden data の読み込みに失敗しました: {path}");
	}

	private static Board BoardFromJson(int[][] rows)
	{
		var board = new Board();
		for (int r = 0; r < Board.BoardSize; r++)
			for (int c = 0; c < Board.BoardSize; c++)
				board.SetPiece(r, c, (PlayerColor)rows[r][c]);
		return board;
	}

	/// <summary>
	/// time_ms を含まない（固定深さ）全レコードで、AlphaBetaAI の着手が
	/// Python/Rust の golden 着手と一致することを確認する。
	/// パス条件: 全レコードで着手が一致し、検証件数が 1 件以上であること。
	/// </summary>
	[Fact]
	public void GetBestMove_FixedDepthRecords_MatchGoldenMoves()
	{
		var records = LoadGoldenRecords().Where(r => r.TimeMs is null).ToList();
		var ai = new AlphaBetaAI();
		int checkedCount = 0;

		foreach (var record in records)
		{
			// 有効手なし局面のスナップショットは対象外（GetBestMoveAtDepthForTest は例外を投げる設計）
			if (record.Move is not { Length: 2 } m)
				continue;

			var board = BoardFromJson(record.Board);
			var player = (PlayerColor)record.Player;
			var expected = new Position(m[0], m[1]);

			var actual = ai.GetBestMoveAtDepthForTest(board, player, record.Depth);

			Assert.True(expected == actual,
				$"不一致: depth={record.Depth} player={record.Player} expected={expected} actual={actual}");
			checkedCount++;
		}

		Assert.True(checkedCount > 0);
	}
}
