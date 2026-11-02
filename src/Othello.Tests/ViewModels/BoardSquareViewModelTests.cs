namespace Technopro.Othello.Tests.ViewModels;

using System.ComponentModel;
using Technopro.Othello.Core.Models;
using Technopro.Othello.ViewModels;

/// <summary>
/// BoardSquareViewModel の単体テスト。
/// 石の設定・プロパティ変更通知・各フラグの初期値を検証する。
/// </summary>
public class BoardSquareViewModelTests
{
	// ========== コンストラクタ ==========

	/// <summary>
	/// 指定した Position がそのまま Position プロパティに格納されることを確認する。
	/// パス条件: Position.Row=3, Col=5 を渡すとプロパティが同じ値を返すこと。
	/// </summary>
	[Fact]
	public void Constructor_SetsPosition()
	{
		var pos = new Position(3, 5);
		var sq = new BoardSquareViewModel(pos);
		Assert.Equal(pos, sq.Position);
	}

	// ========== 初期値 ==========

	/// <summary>
	/// 初期状態で Piece が Empty であることを確認する。
	/// パス条件: 生成直後の Piece が PlayerColor.Empty であること。
	/// </summary>
	[Fact]
	public void Piece_DefaultEmpty()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.Equal(PlayerColor.Empty, sq.Piece);
	}

	/// <summary>
	/// 初期状態で HasPiece が false であることを確認する。
	/// パス条件: 生成直後の HasPiece が false であること。
	/// </summary>
	[Fact]
	public void HasPiece_DefaultFalse()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.False(sq.HasPiece);
	}

	/// <summary>
	/// 初期状態で IsValidMove が false であることを確認する。
	/// パス条件: 生成直後の IsValidMove が false であること。
	/// </summary>
	[Fact]
	public void IsValidMove_DefaultFalse()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.False(sq.IsValidMove);
	}

	/// <summary>
	/// 初期状態で IsBeingFlipped が false であることを確認する。
	/// パス条件: 生成直後の IsBeingFlipped が false であること。
	/// </summary>
	[Fact]
	public void IsBeingFlipped_DefaultFalse()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.False(sq.IsBeingFlipped);
	}

	// ========== SetPiece ==========

	/// <summary>
	/// SetPiece(Black) を呼ぶと Piece=Black, HasPiece=true になることを確認する。
	/// パス条件: Piece が Black, HasPiece が true であること。
	/// </summary>
	[Fact]
	public void SetPiece_Black_SetsPieceAndHasPiece()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		sq.SetPiece(PlayerColor.Black);
		Assert.Equal(PlayerColor.Black, sq.Piece);
		Assert.True(sq.HasPiece);
	}

	/// <summary>
	/// SetPiece(White) を呼ぶと Piece=White, HasPiece=true になることを確認する。
	/// パス条件: Piece が White, HasPiece が true であること。
	/// </summary>
	[Fact]
	public void SetPiece_White_SetsPieceAndHasPiece()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		sq.SetPiece(PlayerColor.White);
		Assert.Equal(PlayerColor.White, sq.Piece);
		Assert.True(sq.HasPiece);
	}

	/// <summary>
	/// 一度石を置いた後 SetPiece(Empty) を呼ぶと Piece=Empty, HasPiece=false になることを確認する。
	/// パス条件: Piece が Empty, HasPiece が false であること。
	/// </summary>
	[Fact]
	public void SetPiece_Empty_ClearsPiece()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		sq.SetPiece(PlayerColor.Black);
		sq.SetPiece(PlayerColor.Empty);
		Assert.Equal(PlayerColor.Empty, sq.Piece);
		Assert.False(sq.HasPiece);
	}

	// ========== PropertyChanged ==========

	/// <summary>
	/// SetPiece 呼び出し時に "Piece" と "HasPiece" の PropertyChanged が発火することを確認する。
	/// パス条件: "Piece" と "HasPiece" が changed リストに含まれること。
	/// </summary>
	[Fact]
	public void SetPiece_RaisesPropertyChangedForPieceAndHasPiece()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		var changed = new List<string?>();
		((INotifyPropertyChanged)sq).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		sq.SetPiece(PlayerColor.Black);

		Assert.Contains("Piece", changed);
		Assert.Contains("HasPiece", changed);
	}

	/// <summary>
	/// IsValidMove を変更すると "IsValidMove" の PropertyChanged が発火することを確認する。
	/// パス条件: "IsValidMove" の PropertyChanged が発火すること。
	/// </summary>
	[Fact]
	public void IsValidMove_RaisesPropertyChanged()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		var changed = new List<string?>();
		((INotifyPropertyChanged)sq).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

		sq.IsValidMove = true;

		Assert.Contains("IsValidMove", changed);
	}

	/// <summary>
	/// IsBeingFlipped を変更すると "IsBeingFlipped" の PropertyChanged が発火することを確認する。
	/// パス条件: "IsBeingFlipped" の PropertyChanged が発火すること。
	/// </summary>
	[Fact]
	public void IsBeingFlipped_RaisesPropertyChanged()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		string? changedProp = null;
		((INotifyPropertyChanged)sq).PropertyChanged += (_, e) => changedProp = e.PropertyName;

		sq.IsBeingFlipped = true;

		Assert.Equal("IsBeingFlipped", changedProp);
	}

	// ========== IsHint ==========

	/// <summary>
	/// 初期状態で IsHint が false であることを確認する。
	/// パス条件: 生成直後の IsHint が false であること。
	/// </summary>
	[Fact]
	public void IsHint_DefaultFalse()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.False(sq.IsHint);
	}

	/// <summary>
	/// IsHint を変更すると "IsHint" の PropertyChanged が発火することを確認する。
	/// パス条件: "IsHint" の PropertyChanged が発火すること。
	/// </summary>
	[Fact]
	public void IsHint_RaisesPropertyChanged()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		string? changedProp = null;
		((INotifyPropertyChanged)sq).PropertyChanged += (_, e) => changedProp = e.PropertyName;

		sq.IsHint = true;

		Assert.Equal("IsHint", changedProp);
	}

	// ========== IsLastMove ==========

	/// <summary>
	/// 初期状態で IsLastMove が false であることを確認する。
	/// パス条件: 生成直後の IsLastMove が false であること。
	/// </summary>
	[Fact]
	public void IsLastMove_DefaultFalse()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		Assert.False(sq.IsLastMove);
	}

	/// <summary>
	/// IsLastMove を変更すると "IsLastMove" の PropertyChanged が発火することを確認する。
	/// パス条件: "IsLastMove" の PropertyChanged が発火すること。
	/// </summary>
	[Fact]
	public void IsLastMove_RaisesPropertyChanged()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		string? changedProp = null;
		((INotifyPropertyChanged)sq).PropertyChanged += (_, e) => changedProp = e.PropertyName;

		sq.IsLastMove = true;

		Assert.Equal("IsLastMove", changedProp);
	}

	// ========== AccessibleName（スクリーンリーダー向け名称, Issue #225） ==========

	/// <summary>
	/// 空きマスで着手不可の場合、座標と「空き」のみを読み上げることを確認する。
	/// パス条件: Position(3,3) の初期状態で AccessibleName が "d4 空き" になること。
	/// </summary>
	[Fact]
	public void AccessibleName_EmptySquare_ReturnsNotationAndEmpty()
	{
		var sq = new BoardSquareViewModel(new Position(3, 3));
		Assert.Equal("d4 空き", sq.AccessibleName);
	}

	/// <summary>
	/// 石があるマスでは石の色が読み上げられることを確認する。
	/// パス条件: 黒石 → "a1 黒"、白石 → "h8 白" になること。
	/// </summary>
	[Fact]
	public void AccessibleName_WithPiece_ReturnsNotationAndColor()
	{
		var black = new BoardSquareViewModel(new Position(0, 0));
		black.SetPiece(PlayerColor.Black);
		var white = new BoardSquareViewModel(new Position(7, 7));
		white.SetPiece(PlayerColor.White);

		Assert.Equal("a1 黒", black.AccessibleName);
		Assert.Equal("h8 白", white.AccessibleName);
	}

	/// <summary>
	/// 着手可能な空きマスでは「打てます」が付くことを確認する。
	/// パス条件: IsValidMove = true で AccessibleName が "c4 空き 打てます" になること。
	/// </summary>
	[Fact]
	public void AccessibleName_ValidMove_AppendsPlayable()
	{
		var sq = new BoardSquareViewModel(new Position(3, 2)) { IsValidMove = true };
		Assert.Equal("c4 空き 打てます", sq.AccessibleName);
	}

	/// <summary>
	/// 石の設定・着手可否の変更で AccessibleName の変更通知が発火することを確認する。
	/// パス条件: SetPiece と IsValidMove の変更でそれぞれ "AccessibleName" の PropertyChanged が発火すること。
	/// </summary>
	[Fact]
	public void AccessibleName_RaisesPropertyChanged_WhenPieceOrValidMoveChanges()
	{
		var sq = new BoardSquareViewModel(new Position(0, 0));
		var names = new List<string?>();
		sq.PropertyChanged += (_, e) => names.Add(e.PropertyName);

		sq.SetPiece(PlayerColor.Black);
		sq.IsValidMove = true;

		Assert.Equal(2, names.Count(n => n == "AccessibleName"));
	}
}
