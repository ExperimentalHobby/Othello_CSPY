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

	/// <inheritdoc/>
	public void Save(GameStats stats)
	{
		var dir = Path.GetDirectoryName(_filePath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(_filePath, JsonSerializer.Serialize(stats, Options));
	}

	/// <inheritdoc/>
	public void Reset() => Save(new GameStats());
}
