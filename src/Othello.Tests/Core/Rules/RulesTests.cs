namespace Technopro.Othello.Tests.Core.Rules;

using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;

public class OthelloRulesTests
{
	/// <summary>
	/// 初期盤面で黒の有効手が正しく 4 件取得できることを確認する。
	/// パス条件: 件数が 4 かつ (3,2)(2,3)(4,5)(5,4) の 4 座標をすべて含むこと。
	/// </summary>
	[Fact]
	public void GetValidMoves_BlackStartPosition_Returns4Moves()
	{
		var board = new Board();
		var moves = OthelloRules.GetValidMoves(board, PlayerColor.Black);
		Assert.Equal(4, moves.Count);
		Assert.Contains(new Position(3, 2), moves);
		Assert.Contains(new Position(2, 3), moves);
		Assert.Contains(new Position(4, 5), moves);
		Assert.Contains(new Position(5, 4), moves);
	}

	/// <summary>
	/// 初期盤面において黒の有効手 (3,2) が IsValidMove で true と判定されることを確認する。
	/// パス条件: IsValidMove の戻り値が true であること。
	/// </summary>
	[Fact]
	public void IsValidMove_ValidPosition_ReturnsTrue()
	{
		var board = new Board();
		Assert.True(OthelloRules.IsValidMove(board, new Position(3, 2), PlayerColor.Black));
	}

	/// <summary>
	/// 石を挟めない座標（隅 (0,0)）は IsValidMove で false と判定されることを確認する。
	/// パス条件: IsValidMove の戻り値が false であること。
	/// </summary>
	[Fact]
	public void IsValidMove_InvalidPosition_ReturnsFalse()
	{
		var board = new Board();
		Assert.False(OthelloRules.IsValidMove(board, new Position(0, 0), PlayerColor.Black));
	}

	/// <summary>
	/// 有効手に着手すると盤面が更新され黒の石数が増加することを確認する。
	/// パス条件: MakeMove 後の CountPieces(Black) が着手前より大きいこと。
	/// </summary>
	[Fact]
	public void MakeMove_ValidMove_UpdatesBoard()
	{
		var board = new Board();
		int beforeCount = board.CountPieces(PlayerColor.Black);
		OthelloRules.MakeMove(board, new Position(3, 2), PlayerColor.Black);
		Assert.True(board.CountPieces(PlayerColor.Black) > beforeCount);
	}

	/// <summary>
	/// 初期盤面では黒に有効手があるためパスできない（CanPass = false）ことを確認する。
	/// パス条件: CanPass の戻り値が false であること。
	/// </summary>
	[Fact]
	public void CanPass_InitialBoard_ReturnsFalse()
	{
		var board = new Board();
		Assert.False(OthelloRules.CanPass(board, PlayerColor.Black));
	}

	/// <summary>
	/// 初期盤面では両者に有効手があるためゲームが終了していない（IsGameOver = false）ことを確認する。
	/// パス条件: IsGameOver の戻り値が false であること。
	/// </summary>
	[Fact]
	public void IsGameOver_InitialBoard_ReturnsFalse()
	{
		var board = new Board();
		Assert.False(OthelloRules.IsGameOver(board));
	}

	/// <summary>
	/// 盤面が完全に埋まっている場合、両者ともに有効手がなく IsGameOver = true になることを確認する。
	/// パス条件: 全マス黒の盤面で IsGameOver が true であること。
	/// </summary>
	[Fact]
	public void IsGameOver_FullBoard_ReturnsTrue()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Black);

		Assert.True(OthelloRules.IsGameOver(board));
	}

	/// <summary>
	/// 自分の石を 1 枚も持たないプレイヤーは有効手がなく CanPass = true になることを確認する。
	/// パス条件: 全マス黒の盤面で白の CanPass が true、IsGameOver も true であること。
	/// </summary>
	[Fact]
	public void CanPass_WhenNoValidMoves_ReturnsTrue()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Black);

		Assert.True(OthelloRules.CanPass(board, PlayerColor.White));
		Assert.True(OthelloRules.IsGameOver(board));
	}

	/// <summary>
	/// 有効でない位置への MakeMove は InvalidOperationException を投げることを確認する。
	/// パス条件: 初期盤面の (0,0) に黒で打つと InvalidOperationException がスローされること。
	/// </summary>
	[Fact]
	public void MakeMove_InvalidPosition_ThrowsInvalidOperationException()
	{
		var board = new Board();
		Assert.Throws<InvalidOperationException>(
			() => OthelloRules.MakeMove(board, new Position(0, 0), PlayerColor.Black));
	}
}

public class GetGameResultTests
{
	/// <summary>
	/// 黒の石数が多い盤面では黒が勝者として返ることを確認する。
	/// パス条件: winner が Black、blackScore=63、whiteScore=1 であること。
	/// </summary>
	[Fact]
	public void GetGameResult_BlackMajority_ReturnsBlackWinner()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Black);
		board.SetPiece(0, 0, PlayerColor.White); // White=1, Black=63

		var (winner, blackScore, whiteScore) = OthelloRules.GetGameResult(board);

		Assert.Equal(PlayerColor.Black, winner);
		Assert.Equal(63, blackScore);
		Assert.Equal(1, whiteScore);
	}

	/// <summary>
	/// 白の石数が多い盤面では白が勝者として返ることを確認する。
	/// パス条件: winner が White、blackScore=1、whiteScore=63 であること。
	/// </summary>
	[Fact]
	public void GetGameResult_WhiteMajority_ReturnsWhiteWinner()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.White);
		board.SetPiece(0, 0, PlayerColor.Black); // Black=1, White=63

		var (winner, blackScore, whiteScore) = OthelloRules.GetGameResult(board);

		Assert.Equal(PlayerColor.White, winner);
		Assert.Equal(1, blackScore);
		Assert.Equal(63, whiteScore);
	}

	/// <summary>
	/// 黒白が同数の盤面では winner が null（引き分け）として返ることを確認する。
	/// パス条件: winner が null、blackScore=32、whiteScore=32 であること。
	/// </summary>
	[Fact]
	public void GetGameResult_EqualScore_ReturnsNullWinner()
	{
		// 上半分 32 マスを黒、下半分 32 マスを白で埋める
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, r < 4 ? PlayerColor.Black : PlayerColor.White);

		var (winner, blackScore, whiteScore) = OthelloRules.GetGameResult(board);

		Assert.Null(winner);
		Assert.Equal(32, blackScore);
		Assert.Equal(32, whiteScore);
	}
}

public class FlipCalculatorTests
{
	/// <summary>
	/// 初期盤面で黒が (3,2) に打つと白 (3,3) を反転できることを確認する。
	/// パス条件: 反転リストが 1 件かつ (3,3) を含むこと。
	/// </summary>
	[Fact]
	public void GetFlippablePieces_ValidPosition_ReturnsFlippedPieces()
	{
		var board = new Board();
		var flipped = FlipCalculator.GetFlippablePieces(board, new Position(3, 2), PlayerColor.Black);
		Assert.Single(flipped);
		Assert.Contains(new Position(3, 3), flipped);
	}

	/// <summary>
	/// 石を挟めない座標（隅 (0,0)）では反転リストが空であることを確認する。
	/// パス条件: GetFlippablePieces の戻り値が空コレクションであること。
	/// </summary>
	[Fact]
	public void GetFlippablePieces_NoFlips_ReturnsEmpty()
	{
		var board = new Board();
		var flipped = FlipCalculator.GetFlippablePieces(board, new Position(0, 0), PlayerColor.Black);
		Assert.Empty(flipped);
	}

	/// <summary>
	/// 複数方向で挟める座標への着手では、全方向の反転石が合計で返ることを確認する。
	/// 盤面: (0,0)=黒, (1,1)=白, (2,2)=白, (3,0)=黒, (3,1)=白, (3,2)=白 → 黒が (3,3) に打つ。
	///   左方向:      (3,2)=白, (3,1)=白, (3,0)=黒 → (3,2)(3,1) を反転
	///   左斜め上方向: (2,2)=白, (1,1)=白, (0,0)=黒 → (2,2)(1,1) を反転
	/// パス条件: 反転リストが 4 件で {(1,1),(2,2),(3,1),(3,2)} の座標をすべて含むこと。
	/// </summary>
	[Fact]
	public void GetFlippablePieces_MultipleDirections_ReturnsAllFlipped()
	{
		var board = new Board();
		// 全マスを空にしてから必要な石だけ配置する
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Empty);

		board.SetPiece(0, 0, PlayerColor.Black);  // 左斜め上方向の終端
		board.SetPiece(1, 1, PlayerColor.White);  // 左斜め上方向の反転候補
		board.SetPiece(2, 2, PlayerColor.White);  // 左斜め上方向の反転候補
		board.SetPiece(3, 0, PlayerColor.Black);  // 左方向の終端
		board.SetPiece(3, 1, PlayerColor.White);  // 左方向の反転候補
		board.SetPiece(3, 2, PlayerColor.White);  // 左方向の反転候補

		var flipped = FlipCalculator.GetFlippablePieces(board, new Position(3, 3), PlayerColor.Black);

		Assert.Equal(4, flipped.Count);
		Assert.Contains(new Position(1, 1), flipped);
		Assert.Contains(new Position(2, 2), flipped);
		Assert.Contains(new Position(3, 1), flipped);
		Assert.Contains(new Position(3, 2), flipped);
	}

	// ---- HasAnyFlip 短絡判定 -----------------------------------------------

	/// <summary>
	/// 初期盤面で黒が (3,2) に打てる（少なくとも 1 方向挟める）ことを HasAnyFlip で確認する。
	/// パス条件: HasAnyFlip の戻り値が true であること。
	/// </summary>
	[Fact]
	public void HasAnyFlip_ValidMove_ReturnsTrue()
	{
		var board = new Board();
		Assert.True(FlipCalculator.HasAnyFlip(board, new Position(3, 2), PlayerColor.Black));
	}

	/// <summary>
	/// 初期盤面の隅 (0,0) は黒でも挟めないため HasAnyFlip が false を返すことを確認する。
	/// パス条件: HasAnyFlip の戻り値が false であること。
	/// </summary>
	[Fact]
	public void HasAnyFlip_InvalidMove_ReturnsFalse()
	{
		var board = new Board();
		Assert.False(FlipCalculator.HasAnyFlip(board, new Position(0, 0), PlayerColor.Black));
	}

	/// <summary>
	/// 既に石が置かれているマス (3,3) は HasAnyFlip が false を返すことを確認する。
	/// パス条件: HasAnyFlip の戻り値が false であること。
	/// </summary>
	[Fact]
	public void HasAnyFlip_OccupiedSquare_ReturnsFalse()
	{
		var board = new Board();
		// (3,3) は初期配置で White が存在する
		Assert.False(FlipCalculator.HasAnyFlip(board, new Position(3, 3), PlayerColor.Black));
	}

	/// <summary>
	/// HasAnyFlip の結果が GetFlippablePieces.Count > 0 と常に一致することを確認する。
	/// 初期盤面の全空きマスで比較する。
	/// パス条件: 両者の結果が一致すること。
	/// </summary>
	[Fact]
	public void HasAnyFlip_AlwaysMatchesGetFlippablePiecesCount()
	{
		var board = new Board();
		for (int r = 0; r < 8; r++)
		{
			for (int c = 0; c < 8; c++)
			{
				var pos = new Position(r, c);
				bool hasAny = FlipCalculator.HasAnyFlip(board, pos, PlayerColor.Black);
				bool hasFlips = FlipCalculator.GetFlippablePieces(board, pos, PlayerColor.Black).Count > 0;
				Assert.Equal(hasFlips, hasAny);
			}
		}
	}
}
