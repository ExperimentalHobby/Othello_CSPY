namespace Technopro.Othello.Core.Kifu;

using Technopro.Othello.Core.Models;

/// <summary>
/// 棋譜の 1 手を表すレコード。
/// パスの場合は IsPass = true とし、Row / Col は null にする。
/// </summary>
public record KifuMove(
    PlayerColor Player,
    int? Row = null,
    int? Col = null,
    bool IsPass = false);
