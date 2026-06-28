namespace Technopro.Othello.Core.Kifu;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;

/// <summary>
/// 1 局分の棋譜データを保持するレコード。
/// KifuSerializer で JSON に変換して保存・読み込みする。
/// </summary>
public record KifuRecord(
    int Version,
    DateTimeOffset PlayedAt,
    PlayerColor HumanColor,
    DifficultyLevel Difficulty,
    PlayerColor? Result,
    IReadOnlyList<KifuMove> Moves,
    KifuFinalScore FinalScore);
