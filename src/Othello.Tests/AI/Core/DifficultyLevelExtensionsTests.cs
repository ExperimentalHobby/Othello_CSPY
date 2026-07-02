namespace Technopro.Othello.Tests.Core.AI;

using Technopro.Othello.Core.AI;

/// <summary>
/// DifficultyLevelExtensions の拡張メソッドを全難易度・全メソッドで検証するテスト群。
/// </summary>
public class DifficultyLevelExtensionsTests
{
    // ---- GetSearchDepth -------------------------------------------------------

    /// <summary>
    /// Easy の探索深さが 2 であることを確認する。
    /// パス条件: GetSearchDepth() が 2 を返すこと。
    /// </summary>
    [Fact]
    public void GetSearchDepth_Easy_Returns2()
    {
        Assert.Equal(2, DifficultyLevel.Easy.GetSearchDepth());
    }

    /// <summary>
    /// Medium の探索深さが 5 であることを確認する。
    /// パス条件: GetSearchDepth() が 5 を返すこと。
    /// </summary>
    [Fact]
    public void GetSearchDepth_Medium_Returns5()
    {
        Assert.Equal(5, DifficultyLevel.Medium.GetSearchDepth());
    }

    /// <summary>
    /// Hard の探索深さが 10 であることを確認する。
    /// パス条件: GetSearchDepth() が 10 を返すこと。
    /// </summary>
    [Fact]
    public void GetSearchDepth_Hard_Returns10()
    {
        Assert.Equal(10, DifficultyLevel.Hard.GetSearchDepth());
    }

    // ---- GetTimeLimitMs -------------------------------------------------------

    /// <summary>
    /// Easy は固定深さ探索のため GetTimeLimitMs が null を返す。
    /// パス条件: 戻り値が null であること。
    /// </summary>
    [Fact]
    public void GetTimeLimitMs_Easy_ReturnsNull()
    {
        Assert.Null(DifficultyLevel.Easy.GetTimeLimitMs());
    }

    /// <summary>
    /// Medium は固定深さ探索のため GetTimeLimitMs が null を返す。
    /// パス条件: 戻り値が null であること。
    /// </summary>
    [Fact]
    public void GetTimeLimitMs_Medium_ReturnsNull()
    {
        Assert.Null(DifficultyLevel.Medium.GetTimeLimitMs());
    }

    /// <summary>
    /// Hard は反復深化探索のため GetTimeLimitMs が 8000 ms を返す。
    /// パス条件: 戻り値が 8000 であること。
    /// </summary>
    [Fact]
    public void GetTimeLimitMs_Hard_Returns8000()
    {
        Assert.Equal(8000, DifficultyLevel.Hard.GetTimeLimitMs());
    }

    // ---- ToDisplayString -------------------------------------------------------

    /// <summary>
    /// Easy の表示文字列が "イージー" であることを確認する。
    /// パス条件: ToDisplayString() が "イージー" を返すこと。
    /// </summary>
    [Fact]
    public void ToDisplayString_Easy_ReturnsCorrectString()
    {
        Assert.Equal("イージー", DifficultyLevel.Easy.ToDisplayString());
    }

    /// <summary>
    /// Medium の表示文字列が "ノーマル" であることを確認する。
    /// パス条件: ToDisplayString() が "ノーマル" を返すこと。
    /// </summary>
    [Fact]
    public void ToDisplayString_Medium_ReturnsCorrectString()
    {
        Assert.Equal("ノーマル", DifficultyLevel.Medium.ToDisplayString());
    }

    /// <summary>
    /// Hard の表示文字列が "ハード" であることを確認する。
    /// パス条件: ToDisplayString() が "ハード" を返すこと。
    /// </summary>
    [Fact]
    public void ToDisplayString_Hard_ReturnsCorrectString()
    {
        Assert.Equal("ハード", DifficultyLevel.Hard.ToDisplayString());
    }

    /// <summary>
    /// 未定義の DifficultyLevel 値は ToDisplayString の default 分岐で "不明" を返す。
    /// パス条件: ToDisplayString() が "不明" を返すこと。
    /// </summary>
    [Fact]
    public void ToDisplayString_InvalidDifficulty_ReturnsUnknown()
    {
        Assert.Equal("不明", ((DifficultyLevel)99).ToDisplayString());
    }

    /// <summary>
    /// 未定義の DifficultyLevel 値は GetSearchDepth の default 分岐で 5 を返す。
    /// パス条件: GetSearchDepth() が 5 を返すこと。
    /// </summary>
    [Fact]
    public void GetSearchDepth_InvalidDifficulty_ReturnsDefault5()
    {
        Assert.Equal(5, ((DifficultyLevel)99).GetSearchDepth());
    }

    // ---- Beginner / Expert 追加 -----------------------------------------------

    /// <summary>パス条件: Beginner の探索深さが 1 であること。</summary>
    [Fact]
    public void GetSearchDepth_Beginner_Returns1()
    {
        Assert.Equal(1, DifficultyLevel.Beginner.GetSearchDepth());
    }

    /// <summary>パス条件: Expert の探索深さが 12 であること。</summary>
    [Fact]
    public void GetSearchDepth_Expert_Returns12()
    {
        Assert.Equal(12, DifficultyLevel.Expert.GetSearchDepth());
    }

    /// <summary>パス条件: Beginner は時間制限なし（null）であること。</summary>
    [Fact]
    public void GetTimeLimitMs_Beginner_ReturnsNull()
    {
        Assert.Null(DifficultyLevel.Beginner.GetTimeLimitMs());
    }

    /// <summary>パス条件: Expert の時間制限が 15000 ms であること。</summary>
    [Fact]
    public void GetTimeLimitMs_Expert_Returns15000()
    {
        Assert.Equal(15000, DifficultyLevel.Expert.GetTimeLimitMs());
    }

    /// <summary>パス条件: Beginner の表示文字列が "ビギナー" であること。</summary>
    [Fact]
    public void ToDisplayString_Beginner_ReturnsCorrectString()
    {
        Assert.Equal("ビギナー", DifficultyLevel.Beginner.ToDisplayString());
    }

    /// <summary>パス条件: Expert の表示文字列が "エキスパート" であること。</summary>
    [Fact]
    public void ToDisplayString_Expert_ReturnsCorrectString()
    {
        Assert.Equal("エキスパート", DifficultyLevel.Expert.ToDisplayString());
    }
}
