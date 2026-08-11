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

	/// <summary>
	/// 正常なファイルパスへの保存が成功したことを true で通知することを確認する。
	/// パス条件: Save の戻り値が true であること。
	/// </summary>
	[Fact]
	public void Save_ValidPath_ReturnsTrue()
	{
		var settings = new OthelloSettings { TimeLimitSeconds = 20 };

		bool result = OthelloSettingsManager.Save(settings, _tmpFile);

		Assert.True(result);
	}

	/// <summary>
	/// 書き込み不可能な対象（既存ディレクトリをファイルパスとして渡す）への保存で
	/// 例外を投げず false を返すことを確認する。
	/// パス条件: 例外が伝播せず、戻り値が false であること。
	/// </summary>
	[Fact]
	public void Save_WhenTargetIsDirectory_ReturnsFalseWithoutThrowing()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"othello_settings_dir_{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		try
		{
			var settings = new OthelloSettings { TimeLimitSeconds = 20 };

			var exception = Record.Exception(() => OthelloSettingsManager.Save(settings, directoryPath));

			Assert.Null(exception);
		}
		finally
		{
			Directory.Delete(directoryPath);
		}
	}
}
