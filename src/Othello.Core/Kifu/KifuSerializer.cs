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
        WriteIndented       = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters          = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>KifuRecord を JSON 文字列にシリアライズして返す。</summary>
    public static string Serialize(KifuRecord record) =>
        JsonSerializer.Serialize(record, Options);

    /// <summary>
    /// JSON 文字列を KifuRecord にデシリアライズして返す。
    /// 不正な JSON や変換失敗の場合は null を返す（例外をスローしない）。
    /// </summary>
    public static KifuRecord? Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<KifuRecord>(json, Options);
        }
        catch (JsonException)
        {
            return null;
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
