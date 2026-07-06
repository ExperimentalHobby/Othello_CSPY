namespace Technopro.Othello.Core.Settings;

/// <summary>
/// アプリケーション設定を保持するモデル。
/// OthelloSettingsManager で JSON ファイルに永続化される。
/// </summary>
public class OthelloSettings
{
	/// <summary>制限時間のデフォルト値（秒）。</summary>
	public const int DefaultTimeLimitSeconds = 30;

	/// <summary>人間プレイヤーの 1 手あたりの制限時間（秒）。</summary>
	public int TimeLimitSeconds { get; set; } = DefaultTimeLimitSeconds;
}
