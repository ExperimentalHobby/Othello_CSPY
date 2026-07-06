namespace Technopro.Othello.Core.Settings;

using System.Text.Json;

/// <summary>
/// OthelloSettings を JSON ファイルに保存・読み込みする静的クラス。
/// ファイルが存在しない・不正 JSON の場合はデフォルト値を返す（例外をスローしない）。
/// </summary>
public static class OthelloSettingsManager
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>
	/// デフォルトの設定ファイルパスを返す（%LOCALAPPDATA%\OthelloCspy\settings.json）。
	/// </summary>
	public static string DefaultFilePath { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"OthelloCspy", "settings.json");

	/// <summary>
	/// 指定ファイルから設定を読み込む。
	/// ファイルが存在しない・読み込み失敗の場合はデフォルト値を返す。
	/// </summary>
	public static OthelloSettings Load(string? filePath = null)
	{
		filePath ??= DefaultFilePath;
		if (!File.Exists(filePath))
			return new OthelloSettings();
		try
		{
			var json = File.ReadAllText(filePath);
			return JsonSerializer.Deserialize<OthelloSettings>(json, Options)
				   ?? new OthelloSettings();
		}
		catch
		{
			return new OthelloSettings();
		}
	}

	/// <summary>
	/// 設定を指定ファイルへ保存する。
	/// ディレクトリが存在しない場合は自動作成する。
	/// </summary>
	public static void Save(OthelloSettings settings, string? filePath = null)
	{
		filePath ??= DefaultFilePath;
		var dir = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
	}
}
