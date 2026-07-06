namespace Technopro.Othello.Core.Stats;

/// <summary>
/// ゲーム統計の読み書きを抽象化するリポジトリインターフェース。
/// テスト時はインメモリ実装を注入できる。
/// </summary>
public interface IStatsRepository
{
	/// <summary>統計を読み込む。ファイル不在・破損時は空の GameStats を返す。</summary>
	GameStats Load();

	/// <summary>統計を保存する。</summary>
	void Save(GameStats stats);

	/// <summary>統計を初期状態にリセットする。</summary>
	void Reset();
}
