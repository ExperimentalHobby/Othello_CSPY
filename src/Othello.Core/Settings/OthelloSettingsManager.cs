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
	/// 書き込みに失敗した場合は例外をスローせず false を返す。
	///
	/// 一時ファイルへ書き込んでから <see cref="File.Move(string, string, bool)"/> で
	/// アトミックに置き換える（Issue #165）。書き込み途中でプロセスが異常終了しても、
	/// 本来のファイルパスには「書き込み前の状態」か「書き込み完了後の状態」のいずれかしか
	/// 現れないため、ファイル破損による設定データの消失を防げる。
	/// </summary>
	/// <returns>保存に成功した場合は true、失敗した場合は false。</returns>
	public static bool Save(OthelloSettings settings, string? filePath = null)
	{
		filePath ??= DefaultFilePath;
		var tempPath = filePath + ".tmp";
		try
		{
			var dir = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
			File.Move(tempPath, filePath, overwrite: true);
			return true;
		}
		catch
		{
			// 一時ファイルが残ると次回以降のディスク容量を無駄に消費するため掃除する（ベストエフォート）。
			try { File.Delete(tempPath); } catch { /* 削除失敗は無視 */ }
			return false;
		}
	}
}
