namespace Technopro.Othello.Tests.AI.Core;

using System.Text.Json;
using System.Text.Json.Serialization;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

/// <summary>
/// Evaluator.Evaluate() / EvaluateFinal() の golden value テスト（Issue #129）。
///
/// evaluator_golden.json（Othello.AI/Python/test_data 由来、ビルド時に本プロジェクトへコピー）は
/// 序盤・中盤・終盤の代表局面と evaluate_final の勝敗パターンについて、Python 実装で計算した
/// 期待評価値を記録したもの。Python/Rust/C# の 3 実装が同じ JSON を参照してそれぞれ独立に
/// 検証することで、評価関数の定数（WEIGHTS・フェーズ閾値・各種係数）が 3 実装間で
/// 一致していることを推移的に確認できる（該当箇所は Evaluator.cs の定数コメント参照）。
/// </summary>
public class EvaluatorGoldenTests
{
	private record GoldenData(
		[property: JsonPropertyName("evaluate_cases")] List<EvaluateCase> EvaluateCases,
		[property: JsonPropertyName("evaluate_final_cases")] List<EvaluateFinalCase> EvaluateFinalCases);

	private record EvaluateCase(string Name, int[][] Board, int Player, int Expected);

	private record EvaluateFinalCase(string Name, int[][] Board, int Player, int Depth, int Expected);

	private static GoldenData LoadGoldenData()
	{
		string path = Path.Combine(AppContext.BaseDirectory, "test_data", "evaluator_golden.json");
		string json = File.ReadAllText(path);
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		return JsonSerializer.Deserialize<GoldenData>(json, options)
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
	/// Evaluator.Evaluate() が全 golden ケースで期待値と一致することを確認する。
	/// パス条件: 全 evaluate_cases で戻り値が Expected と一致すること。
	/// </summary>
	[Fact]
	public void Evaluate_MatchesGoldenValues()
	{
		var data = LoadGoldenData();
		int checkedCount = 0;

		foreach (var c in data.EvaluateCases)
		{
			var board = BoardFromJson(c.Board);
			int actual = Evaluator.Evaluate(board, (PlayerColor)c.Player);
			Assert.True(c.Expected == actual, $"不一致: {c.Name} player={c.Player} expected={c.Expected} actual={actual}");
			checkedCount++;
		}

		Assert.True(checkedCount > 0);
	}

	/// <summary>
	/// Evaluator.EvaluateFinal() が全 golden ケースで期待値と一致することを確認する。
	/// パス条件: 全 evaluate_final_cases で戻り値が Expected と一致すること。
	/// </summary>
	[Fact]
	public void EvaluateFinal_MatchesGoldenValues()
	{
		var data = LoadGoldenData();
		int checkedCount = 0;

		foreach (var c in data.EvaluateFinalCases)
		{
			var board = BoardFromJson(c.Board);
			int actual = Evaluator.EvaluateFinal(board, (PlayerColor)c.Player, c.Depth);
			Assert.True(c.Expected == actual,
				$"不一致: {c.Name} player={c.Player} depth={c.Depth} expected={c.Expected} actual={actual}");
			checkedCount++;
		}

		Assert.True(checkedCount > 0);
	}
}
