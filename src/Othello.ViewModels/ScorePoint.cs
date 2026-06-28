namespace Technopro.Othello.ViewModels;

/// <summary>
/// スコアグラフの 1 データ点。手数と黒・白それぞれの石数を保持する。
/// </summary>
public record ScorePoint(int MoveNumber, int BlackCount, int WhiteCount);
