using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// テスト専用: GameViewModel の internal メンバーへのアクセスを提供する partial クラス。
/// このファイルは Othello.Tests.csproj にのみ組み込む（projitems には含めない）。
/// </summary>
public partial class GameViewModel
{
	/// <summary>
	/// 現在保持している棋譜収集用リストのスナップショット（テスト検証用）。
	/// </summary>
	internal IReadOnlyList<KifuMove> KifuMovesForTest => _kifuMoves;

	/// <summary>
	/// 任意の盤面・手番状態をエンジンに直接ロードする。
	/// 両者に有効手がない終局局面をセットすると EndGame を誘発できる。
	/// </summary>
	internal void LoadStateForTest(Board board, PlayerColor currentPlayer)
	{
		_engine.LoadStateForTest(board, currentPlayer);
		RefreshBoardDisplay();

		var currentMoves = OthelloRules.GetValidMoves(board, currentPlayer);
		var opponent = currentPlayer == PlayerColor.Black ? PlayerColor.White : PlayerColor.Black;
		var opponentMoves = OthelloRules.GetValidMoves(board, opponent);
		if (currentMoves.Count == 0 && opponentMoves.Count == 0)
		{
			EndGame();
			return;
		}

		if (_cts != null)
			_ = CheckAndProcessNextTurnAsync(_cts.Token);
	}
}
