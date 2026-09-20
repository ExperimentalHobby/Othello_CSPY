using System.Diagnostics;
using System.Text.Json;

namespace Technopro.Othello.Core.Stats;

/// <summary>
/// GameStats を JSON ファイルに永続化する実装。
/// ファイルパスをコンストラクタで差し替え可能なため、テストでも使用できる。
/// </summary>
public class StatsRepository : IStatsRepository
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private readonly string _filePath;

	/// <summary>既定の保存先（%LOCALAPPDATA%\OthelloCspy\stats.json）。</summary>
	public static string DefaultFilePath { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"OthelloCspy", "stats.json");

	/// <param name="filePath">保存先ファイルパス。null の場合は DefaultFilePath を使用する。</param>
	public StatsRepository(string? filePath = null)
	{
		_filePath = filePath ?? DefaultFilePath;
	}

	/// <inheritdoc/>
	public GameStats Load()
	{
		try
		{
			if (!File.Exists(_filePath)) return new GameStats();
			var json = File.ReadAllText(_filePath);
			return JsonSerializer.Deserialize<GameStats>(json, Options) ?? new GameStats();
		}
		catch
		{
			return new GameStats();
		}
	}

	/// <summary>
	/// 統計を保存する。書き込みに失敗した場合も例外はスローせず、保存をあきらめて処理を継続する
	/// （統計保存の失敗でゲーム進行を止めないため）。
	///
	/// 一時ファイルへ書き込んでから <see cref="File.Move(string, string, bool)"/> で
	/// アトミックに置き換える（Issue #165）。書き込み途中でプロセスが異常終了しても、
	/// 本来のファイルパスには「書き込み前の状態」か「書き込み完了後の状態」のいずれかしか
	/// 現れないため、ファイル破損による統計データの消失を防げる。
	/// </summary>
	/// <inheritdoc/>
	public void Save(GameStats stats)
	{
		var tempPath = _filePath + ".tmp";
		try
		{
			var dir = Path.GetDirectoryName(_filePath);
			if (!string.IsNullOrEmpty(dir))
				Directory.CreateDirectory(dir);
			File.WriteAllText(tempPath, JsonSerializer.Serialize(stats, Options));
			File.Move(tempPath, _filePath, overwrite: true);
		}
		catch
		{
			// 保存失敗はログに残すのみとし、呼び出し元（GameViewModel）の処理を継続させる。
			Debug.WriteLine($"棋力統計の保存に失敗しました: {_filePath}");
			// 一時ファイルが残ると次回以降のディスク容量を無駄に消費するため掃除する（ベストエフォート）。
			try { File.Delete(tempPath); } catch { /* 削除失敗は無視 */ }
		}
	}

	/// <inheritdoc/>
	public void Reset() => Save(new GameStats());
}
