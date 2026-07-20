namespace Technopro.Othello.Tests.Kifu;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.ViewModels;

/// <summary>
/// KifuViewModel の単体テスト。
/// 再生コントロール（CurrentMove・CanStepForward/Back・StepForward/Back コマンド）を検証する。
/// </summary>
public class KifuViewModelTests
{
	/// <summary>1 手だけの棋譜 KifuViewModel を生成するヘルパー。</summary>
	private static KifuViewModel MakeViewModel()
	{
		var moves = new List<KifuMove>
		{
			new(PlayerColor.Black, Row: 2, Col: 3),
		};
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Easy,
			null, moves, new KifuFinalScore(4, 1));
		return new KifuViewModel(new KifuPlayer(record));
	}

	/// <summary>
	/// 初期状態の CurrentMove が 0 であり、StepForward 後に 1 になることを確認する。
	/// パス条件: 初期値 == 0、StepForward 後 == 1。
	/// </summary>
	[Fact]
	public void CurrentMove_InitiallyZero_ThenOneAfterStepForward()
	{
		var vm = MakeViewModel();
		Assert.Equal(0, vm.CurrentMove);
		vm.StepForwardCommand.Execute(null);
		Assert.Equal(1, vm.CurrentMove);
	}

	/// <summary>
	/// 末尾では CanStepForward が false になることを確認する。
	/// パス条件: GoToEnd 後 CanStepForward == false。
	/// </summary>
	[Fact]
	public void CanStepForward_FalseAtEnd()
	{
		var vm = MakeViewModel();
		vm.StepForwardCommand.Execute(null); // 末尾へ
		Assert.False(vm.CanStepForward);
	}

	/// <summary>
	/// 先頭では CanStepBack が false になることを確認する。
	/// パス条件: 初期状態で CanStepBack == false。
	/// </summary>
	[Fact]
	public void CanStepBack_FalseAtStart()
	{
		var vm = MakeViewModel();
		Assert.False(vm.CanStepBack);
	}

	/// <summary>
	/// StepBack コマンドで CurrentMove が 1 手前に戻ることを確認する。
	/// パス条件: StepForward → StepBack 後に CurrentMove == 0。
	/// </summary>
	[Fact]
	public void StepBack_ReturnsToInitial()
	{
		var vm = MakeViewModel();
		vm.StepForwardCommand.Execute(null);
		vm.StepBackCommand.Execute(null);
		Assert.Equal(0, vm.CurrentMove);
	}

	/// <summary>
	/// GoToStart コマンドで CurrentMove が 0 に戻ることを確認する。
	/// パス条件: GoToEnd 後に GoToStart で CurrentMove == 0。
	/// </summary>
	[Fact]
	public void GoToStart_ResetsCurrentMove()
	{
		var vm = MakeViewModel();
		vm.StepForwardCommand.Execute(null);
		vm.GoToStartCommand.Execute(null);
		Assert.Equal(0, vm.CurrentMove);
	}

	/// <summary>
	/// GoToEnd コマンドで CurrentMove が TotalMoves になることを確認する。
	/// パス条件: GoToEnd 後 CurrentMove == TotalMoves。
	/// </summary>
	[Fact]
	public void GoToEnd_SetsCurrentMoveToTotal()
	{
		var vm = MakeViewModel();
		vm.GoToEndCommand.Execute(null);
		Assert.Equal(vm.TotalMoves, vm.CurrentMove);
	}

	/// <summary>
	/// TotalMoves が棋譜の手数と一致することを確認する。
	/// パス条件: TotalMoves == 1（テスト棋譜は 1 手）。
	/// </summary>
	[Fact]
	public void TotalMoves_MatchesKifuLength()
	{
		var vm = MakeViewModel();
		Assert.Equal(1, vm.TotalMoves);
	}

	/// <summary>
	/// StepForward 後にボード上 (2,3) が Black になることを確認する。
	/// パス条件: BoardSquares[2*8+3].Piece == Black。
	/// </summary>
	[Fact]
	public void BoardSquares_UpdateOnStepForward()
	{
		var vm = MakeViewModel();
		vm.StepForwardCommand.Execute(null);
		var sq = vm.BoardSquares[2 * 8 + 3];
		Assert.Equal(PlayerColor.Black, sq.Piece);
	}

	// ========== #58: 難易度表示・CPU vs CPU 勝敗表示 ==========

	/// <summary>
	/// Beginner 難易度の棋譜を開くと、KifuInfo に日本語表示「ビギナー」が含まれることを確認する。
	/// パス条件: KifuInfo に "ビギナー" が含まれること。
	/// </summary>
	[Fact]
	public void KifuInfo_BeginnerDifficulty_ShowsJapaneseLabel()
	{
		var moves = new List<KifuMove> { new(PlayerColor.Black, Row: 2, Col: 3) };
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Beginner,
			null, moves, new KifuFinalScore(4, 1));
		var vm = new KifuViewModel(new KifuPlayer(record), record);

		Assert.Contains("ビギナー", vm.KifuInfo);
	}

	/// <summary>
	/// Expert 難易度の棋譜を開くと、KifuInfo に日本語表示「エキスパート」が含まれることを確認する。
	/// パス条件: KifuInfo に "エキスパート" が含まれること。
	/// </summary>
	[Fact]
	public void KifuInfo_ExpertDifficulty_ShowsJapaneseLabel()
	{
		var moves = new List<KifuMove> { new(PlayerColor.Black, Row: 2, Col: 3) };
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Expert,
			null, moves, new KifuFinalScore(4, 1));
		var vm = new KifuViewModel(new KifuPlayer(record), record);

		Assert.Contains("エキスパート", vm.KifuInfo);
	}

	/// <summary>
	/// CPU vs CPU で白が勝った棋譜を開くと、色ベースの勝敗表示「白の勝利」になることを確認する
	/// （黒を人間扱いした「あなたの勝利」という誤表示にならないこと）。
	/// パス条件: KifuInfo に "白の勝利" が含まれ、"あなたの勝利" が含まれないこと。
	/// </summary>
	[Fact]
	public void KifuInfo_CpuVsCpuWhiteWins_ShowsColorBasedResult()
	{
		var moves = new List<KifuMove> { new(PlayerColor.Black, Row: 2, Col: 3) };
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Medium,
			PlayerColor.White, moves, new KifuFinalScore(20, 44),
			Mode: GameMode.CpuVsCpu);
		var vm = new KifuViewModel(new KifuPlayer(record), record);

		Assert.Contains("白の勝利", vm.KifuInfo);
		Assert.DoesNotContain("あなたの勝利", vm.KifuInfo);
	}

	// ========== #61: 着手表示を標準記譜法に変更 ==========

	/// <summary>
	/// 着手後の StatusMessage が標準記譜法（例: "d3"）で表示されることを確認する。
	/// パス条件: StatusMessage に "d3" が含まれ、旧形式の "(2,3)" は含まれないこと。
	/// </summary>
	[Fact]
	public void StatusMessage_AfterMove_ShowsStandardNotation()
	{
		var vm = MakeViewModel();
		vm.StepForwardCommand.Execute(null);

		Assert.Contains("d3", vm.StatusMessage);
		Assert.DoesNotContain("(2,3)", vm.StatusMessage);
	}

	/// <summary>
	/// パス手の StatusMessage は記譜法表示への変更後も「パス」表示のままであることを確認する（回帰確認）。
	/// パス条件: StatusMessage に "パス" が含まれること。
	/// </summary>
	[Fact]
	public void StatusMessage_AfterPassMove_StillShowsPassText()
	{
		var moves = new List<KifuMove> { new(PlayerColor.Black, IsPass: true) };
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Easy,
			null, moves, new KifuFinalScore(4, 1));
		var vm = new KifuViewModel(new KifuPlayer(record));

		vm.StepForwardCommand.Execute(null);

		Assert.Contains("パス", vm.StatusMessage);
	}
}
