namespace Technopro.Othello.Core.Kifu;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// KifuRecord を JSON 形式に変換・復元する静的クラス。
/// System.Text.Json を使用し、列挙型は camelCase 文字列でシリアライズする。
/// </summary>
public static class KifuSerializer
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>KifuRecord を JSON 文字列にシリアライズして返す。</summary>
	public static string Serialize(KifuRecord record) =>
		JsonSerializer.Serialize(record, Options);

	/// <summary>
	/// JSON 文字列を KifuRecord にデシリアライズして返す。
	/// 不正な JSON・必須フィールド欠落・非合法な着手を含む棋譜の場合は null を返す（例外をスローしない）。
	/// </summary>
	public static KifuRecord? Deserialize(string json)
	{
		if (string.IsNullOrEmpty(json))
			return null;

		KifuRecord? record;
		try
		{
			record = JsonSerializer.Deserialize<KifuRecord>(json, Options);
		}
		catch (JsonException)
		{
			return null;
		}

		return IsPlayable(record) ? record : null;
	}

	/// <summary>
	/// KifuRecord が実際に再生可能かどうかを検証する。
	/// 手動編集・破損・バージョン間の非互換で必須フィールドが欠落したり非合法な着手を含む棋譜は、
	/// 後続の KifuPlayer 構築（<see cref="KifuPlayer.KifuPlayer(KifuRecord)"/>）で未処理例外を
	/// 起こす代わりにここで弾く（Issue #153）。
	/// </summary>
	private static bool IsPlayable(KifuRecord? record)
	{
		if (record is null || record.Moves is null || record.FinalScore is null)
			return false;

		try
		{
			_ = new KifuPlayer(record);
			return true;
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			return false;
		}
	}

	/// <summary>KifuRecord を指定ファイルパスへ非同期に書き込む。</summary>
	public static async Task SaveAsync(KifuRecord record, string filePath)
	{
		var dir = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		await File.WriteAllTextAsync(filePath, Serialize(record));
	}

	/// <summary>
	/// 指定ファイルパスから KifuRecord を非同期に読み込んで返す。
	/// ファイルが存在しない・不正 JSON の場合は null を返す。
	/// </summary>
	public static async Task<KifuRecord?> LoadAsync(string filePath)
	{
		if (!File.Exists(filePath))
			return null;
		return Deserialize(await File.ReadAllTextAsync(filePath));
	}

	/// <summary>自動保存用のデフォルトディレクトリパスを返す。</summary>
	public static string GetDefaultSaveDirectory() =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"OthelloCspy", "kifu");
}
