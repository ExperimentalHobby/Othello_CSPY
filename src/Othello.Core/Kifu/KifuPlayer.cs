namespace Technopro.Othello.Core.Kifu;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

/// <summary>
/// KifuRecord を手順に沿って再生するプレイヤー。
/// 全ボード状態をコンストラクタで事前計算して保持するため、
/// StepForward / StepBack を O(1) で実行できる。
/// </summary>
public sealed class KifuPlayer
{
	private readonly KifuRecord _kifu;

	/// <summary>
	/// インデックス i のボード状態: _states[0] = 初期盤面、_states[i] = i 手目完了後の盤面。
	/// </summary>
	private readonly IReadOnlyList<Board> _states;

	/// <summary>現在表示している手数（0 = 初期盤面）</summary>
	private int _index;

	/// <summary>現在表示している盤面</summary>
	public Board CurrentBoard => _states[_index];

	/// <summary>現在何手目まで進んだか（0 = 未着手）</summary>
	public int CurrentMoveIndex => _index;

	/// <summary>棋譜の総手数</summary>
	public int TotalMoves => _kifu.Moves.Count;

	/// <summary>直前に実行した手（まだ 0 手目の場合は null）</summary>
	public KifuMove? LastExecutedMove => _index > 0 ? _kifu.Moves[_index - 1] : null;

	/// <summary>次の手に進めるかどうか</summary>
	public bool CanStepForward => _index < TotalMoves;

	/// <summary>前の手に戻れるかどうか</summary>
	public bool CanStepBack => _index > 0;

	/// <summary>
	/// KifuRecord から KifuPlayer を生成する。
	/// コンストラクタで全ボード状態を事前計算する。
	/// </summary>
	public KifuPlayer(KifuRecord kifu)
	{
		_kifu = kifu;
		_states = BuildStates(kifu);
		_index = 0;
	}

	/// <summary>1 手進む。最後の手を超えた場合は no-op。</summary>
	public void StepForward()
	{
		if (CanStepForward) _index++;
	}

	/// <summary>1 手戻る。最初の手より前には戻れない（no-op）。</summary>
	public void StepBack()
	{
		if (CanStepBack) _index--;
	}

	/// <summary>最初の盤面（初期配置）に戻る。</summary>
	public void GoToStart() => _index = 0;

	/// <summary>最後の手まで一気に進む。</summary>
	public void GoToEnd() => _index = TotalMoves;

	/// <summary>
	/// KifuRecord の着手列を初期盤面から順番に再生し、各手完了後のボードを配列に格納する。
	/// パス手は盤面変化なし（ボードのクローンをそのまま追加する）。
	/// </summary>
	private static List<Board> BuildStates(KifuRecord kifu)
	{
		var board = new Board();
		var states = new List<Board>(kifu.Moves.Count + 1) { board.Clone() };

		foreach (var move in kifu.Moves)
		{
			if (!move.IsPass && move.Row.HasValue && move.Col.HasValue)
				OthelloRules.MakeMove(board, new Models.Position(move.Row.Value, move.Col.Value), move.Player);
			// パスは盤面変化なし → クローンをそのまま追加
			states.Add(board.Clone());
		}

		return states;
	}
}
