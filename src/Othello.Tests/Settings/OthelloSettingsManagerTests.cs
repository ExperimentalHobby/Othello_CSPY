namespace Technopro.Othello.Tests.Settings;

using System.IO;
using Technopro.Othello.Core.Settings;

/// <summary>
/// OthelloSettingsManager の単体テスト。
/// JSON 保存・読込の round-trip とデフォルト値を検証する。
/// </summary>
public class OthelloSettingsManagerTests : IDisposable
{
    /// <summary>各テストで使い捨てる一時ファイルパス</summary>
    private readonly string _tmpFile = Path.Combine(
        Path.GetTempPath(),
        $"othello_settings_test_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tmpFile))
            File.Delete(_tmpFile);
    }

    /// <summary>
    /// Save → Load で TimeLimitSeconds が元の値と一致することを確認する。
    /// パス条件: Load 後の TimeLimitSeconds が保存前と等しいこと。
    /// </summary>
    [Fact]
    public void SaveAndLoad_TimeLimitSeconds_RoundTrips()
    {
        var settings = new OthelloSettings { TimeLimitSeconds = 45 };
        OthelloSettingsManager.Save(settings, _tmpFile);

        var loaded = OthelloSettingsManager.Load(_tmpFile);

        Assert.Equal(45, loaded.TimeLimitSeconds);
    }

    /// <summary>
    /// ファイルが存在しない場合にデフォルト値（30 秒）を返すことを確認する。
    /// パス条件: TimeLimitSeconds == 30。
    /// </summary>
    [Fact]
    public void Load_WhenFileNotExists_ReturnsDefault()
    {
        var loaded = OthelloSettingsManager.Load(_tmpFile);

        Assert.Equal(OthelloSettings.DefaultTimeLimitSeconds, loaded.TimeLimitSeconds);
    }

    /// <summary>
    /// 不正な JSON ファイルを読み込んだときもデフォルト値を返すことを確認する。
    /// パス条件: 例外をスローせず TimeLimitSeconds == 30。
    /// </summary>
    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefault()
    {
        File.WriteAllText(_tmpFile, "{ not valid json }");

        var loaded = OthelloSettingsManager.Load(_tmpFile);

        Assert.Equal(OthelloSettings.DefaultTimeLimitSeconds, loaded.TimeLimitSeconds);
    }

    /// <summary>
    /// OthelloSettings のデフォルトコンストラクタで TimeLimitSeconds が 30 になることを確認する。
    /// パス条件: new OthelloSettings().TimeLimitSeconds == 30。
    /// </summary>
    [Fact]
    public void DefaultSettings_TimeLimitSeconds_Is30()
    {
        var settings = new OthelloSettings();
        Assert.Equal(30, settings.TimeLimitSeconds);
    }
}
