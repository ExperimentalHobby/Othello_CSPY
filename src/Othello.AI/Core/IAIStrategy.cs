namespace Technopro.Othello.Core.AI;

using Technopro.Othello.Core.Models;

/// <summary>
/// AI の戦略を抽象化するインターフェース。
/// 実装を差し替えることで、C# 実装の AlphaBetaAI や Python サブプロセス版など
/// 任意の AI バックエンドを利用できる。
/// </summary>
public interface IAIStrategy
{
	/// <summary>
	/// 与えられた盤面と手番に対して AI が最善と判断した着手位置を返す。
	/// </summary>
	/// <param name="board">現在の盤面（変更しない）</param>
	/// <param name="playerColor">AI が担当するプレイヤーの色</param>
	/// <returns>AI が選択した着手先の Position</returns>
	Position GetBestMove(Board board, PlayerColor playerColor);

	/// <summary>この AI インスタンスの難易度レベル</summary>
	DifficultyLevel Difficulty { get; }

	/// <summary>UI に表示する AI バックエンド名（例: "AI: Rust", "AI: Python", "AI: C#"）</summary>
	string EngineName { get; }
}
