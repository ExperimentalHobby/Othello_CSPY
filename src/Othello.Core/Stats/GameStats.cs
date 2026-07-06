using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.Core.Stats;

/// <summary>
/// 全難易度にわたるゲーム統計のルートモデル。
/// RecordResult() で人間プレイヤー視点の対局結果を記録する。
/// </summary>
public class GameStats
{
	/// <summary>データフォーマットバージョン</summary>
	public int Version { get; set; } = 1;

	/// <summary>最終更新日時</summary>
	public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;

	/// <summary>Beginner 難易度の統計</summary>
	public DifficultyStats Beginner { get; set; } = new();

	/// <summary>Easy 難易度の統計</summary>
	public DifficultyStats Easy { get; set; } = new();

	/// <summary>Normal 難易度の統計（DifficultyLevel.Medium に対応）</summary>
	public DifficultyStats Normal { get; set; } = new();

	/// <summary>Hard 難易度の統計</summary>
	public DifficultyStats Hard { get; set; } = new();

	/// <summary>Expert 難易度の統計</summary>
	public DifficultyStats Expert { get; set; } = new();

	/// <summary>最高石数差（人間が勝ったゲームの最大 |黒-白|）</summary>
	public int BestWinMargin { get; set; }

	/// <summary>現在の連勝数（負けまたは引き分けでリセット）</summary>
	public int CurrentStreak { get; set; }

	/// <summary>最大連勝記録</summary>
	public int MaxStreak { get; set; }

	/// <summary>難易度に対応する DifficultyStats を返す。</summary>
	public DifficultyStats GetByDifficulty(DifficultyLevel level) => level switch
	{
		DifficultyLevel.Beginner => Beginner,
		DifficultyLevel.Easy => Easy,
		DifficultyLevel.Medium => Normal,
		DifficultyLevel.Hard => Hard,
		DifficultyLevel.Expert => Expert,
		_ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
	};

	/// <summary>
	/// 人間プレイヤー視点でゲーム結果を記録する。
	/// </summary>
	/// <param name="winner">勝者（null = 引き分け）</param>
	/// <param name="humanColor">人間が担当した色</param>
	/// <param name="difficulty">対局難易度</param>
	/// <param name="moveCount">着手数</param>
	/// <param name="winMargin">石数差の絶対値（勝利時のみ BestWinMargin 更新に使用）</param>
	public void RecordResult(
		PlayerColor? winner,
		PlayerColor humanColor,
		DifficultyLevel difficulty,
		int moveCount,
		int winMargin)
	{
		LastUpdated = DateTimeOffset.Now;
		var diff = GetByDifficulty(difficulty);
		diff.TotalMoves += moveCount;

		if (winner == null)
		{
			diff.Draws++;
			CurrentStreak = 0;
		}
		else if (winner == humanColor)
		{
			diff.Wins++;
			CurrentStreak++;
			MaxStreak = Math.Max(MaxStreak, CurrentStreak);
			BestWinMargin = Math.Max(BestWinMargin, winMargin);
		}
		else
		{
			diff.Losses++;
			CurrentStreak = 0;
		}
	}
}
