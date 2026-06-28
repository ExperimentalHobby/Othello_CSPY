namespace Technopro.Othello.Core.Models;

/// <summary>
/// 対戦モードを表す列挙型。
/// </summary>
public enum GameMode
{
    /// <summary>人間 vs CPU（デフォルト）</summary>
    HumanVsCpu,
    /// <summary>CPU vs CPU（観戦モード）</summary>
    CpuVsCpu,
}
