namespace Technopro.Othello.Core.AI;

/// <summary>
/// AI の難易度レベルを表す列挙型。
/// 難易度に応じてアルファベータ探索の深さと計算時間上限が変わる。
/// </summary>
public enum DifficultyLevel
{
    /// <summary>初級: 探索深さ 2（即座に完了）</summary>
    Easy = 1,
    /// <summary>中級: 探索深さ 5（1〜2 秒程度）</summary>
    Medium = 2,
    /// <summary>上級: 探索深さ 10（3〜5 秒程度）</summary>
    Hard = 3
}

/// <summary>
/// DifficultyLevel 列挙型の拡張メソッドを提供するクラス。
/// </summary>
public static class DifficultyLevelExtensions
{
    /// <summary>
    /// 難易度に応じたアルファベータ探索の最大深さを返す。
    /// Python AI の depth パラメータとして IPC 経由で渡す値でもある。
    /// </summary>
    /// <param name="difficulty">難易度</param>
    /// <returns>探索深さ（Easy:2, Medium:5, Hard:10）</returns>
    public static int GetSearchDepth(this DifficultyLevel difficulty) => difficulty switch
    {
        DifficultyLevel.Easy   => 2,
        DifficultyLevel.Medium => 5,
        DifficultyLevel.Hard   => 10,
        // 想定外の値はデフォルトの中級深さで処理する
        _                      => 5
    };

    /// <summary>
    /// 難易度を日本語の表示文字列に変換する。
    /// </summary>
    /// <param name="difficulty">変換対象の難易度</param>
    /// <returns>"イージー" / "ノーマル" / "ハード" のいずれか</returns>
    public static string ToDisplayString(this DifficultyLevel difficulty) => difficulty switch
    {
        DifficultyLevel.Easy   => "イージー",
        DifficultyLevel.Medium => "ノーマル",
        DifficultyLevel.Hard   => "ハード",
        _                      => "不明"
    };
}
