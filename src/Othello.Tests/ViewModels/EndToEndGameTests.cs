namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Stats;
using Technopro.Othello.ViewModels;

/// <summary>
/// 初手から終局まで一気通貫で検証するエンドツーエンドの結合テスト（Issue #158）。
/// GameEngine（GameViewModel 経由） + StatsViewModel + KifuViewModel（KifuPlayer）を
/// 組み合わせ、機能単位のテストでは検知できない対局全体を通した連携の不整合を検証する。
/// </summary>
public class EndToEndGameTests
{
	// Human vs AI モードは AI 着手ごとに固定の演出待機（AiMoveDelayMs=300ms 等）が入るため、
	// CPU vs CPU モード（CpuVsCpuDelayMs=0 に設定できる）よりも 1 局の所要時間が長くなる。
	// CI 環境でのスケジューリング遅延も見込み、余裕を持った上限を設定する。
	private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

	/// <summary>
	/// 盤面から常に最初の有効手を返す決定的なモック AI。
	/// </summary>
	private sealed class FakeAI : IAIStrategy
	{
		public DifficultyLevel Difficulty { get; }
		public string EngineName => "AI: Fake";
		public FakeAI(DifficultyLevel difficulty) => Difficulty = difficulty;

		public Position GetBestMove(Board board, PlayerColor playerColor) =>
			OthelloRules.GetValidMoves(board, playerColor)[0];
	}

	private sealed class InMemoryStatsRepository : IStatsRepository
	{
		public GameStats Stats = new();
		public GameStats Load() => Stats;
		public void Save(GameStats stats) => Stats = stats;
		public void Reset() => Stats = new GameStats();
	}

	/// <summary>
	/// 人間・AI とも「最初の有効手」を選ぶ決定的な対局を初手から終局まで駆動する。
	/// BoardSquares の IsValidMove は人間のターンのみ立つ（RefreshBoardDisplay の仕様）ため、
	/// これを「人間の手番かどうか」の目印として使い、人間のクリックと AI の応答を交互に進める。
	/// </summary>
	private static async Task PlayFullGameAsync(GameViewModel vm)
	{
		var deadline = DateTime.UtcNow + Deadline;
		while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
		{
			var validSquare = vm.BoardSquares.FirstOrDefault(sq => sq.IsValidMove);
			if (validSquare != null)
				vm.SquareClickedCommand.Execute(validSquare.Position);
			await Task.Delay(20);
		}
	}

	/// <summary>
	/// 初手から終局までを通しで実行し、Winner・統計・棋譜データが実際の対局結果と
	/// すべて整合していることを確認する。
	/// パス条件: ゲーム終了後、(1) LastKifuRecord を KifuPlayer で最後まで再生した盤面が
	/// 実際の最終盤面と完全一致、(2) LastKifuRecord.FinalScore が実際の石数と一致、
	/// (3) LastKifuRecord.Result が実際の勝者と一致、(4) StatsViewModel（Normal 難易度）に
	/// 対局結果が 1 件だけ反映されていること。
	/// </summary>
	[Fact]
	public async Task FullGame_HumanVsAi_WinnerStatsAndKifuAreConsistent()
	{
		var statsRepo = new InMemoryStatsRepository();
		using var vm = new GameViewModel(d => new FakeAI(d), statsRepository: statsRepo);

		await PlayFullGameAsync(vm);

		Assert.False(vm.IsGameInProgress);

		var actualBoard = vm.EngineCurrentBoard;
		var (actualWinner, actualBlack, actualWhite) = OthelloRules.GetGameResult(actualBoard);

		var record = vm.LastKifuRecord;
		Assert.NotNull(record);
		Assert.Equal(actualWinner, record!.Result);
		Assert.Equal(actualBlack, record.FinalScore.Black);
		Assert.Equal(actualWhite, record.FinalScore.White);

		var player = new KifuPlayer(record);
		player.GoToEnd();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				Assert.Equal(actualBoard.GetPiece(r, c), player.CurrentBoard.GetPiece(r, c));

		// 人間=黒（既定）、難易度=Medium（既定）の対局結果が Normal 難易度に 1 件だけ反映される
		var normal = statsRepo.Stats.Normal;
		Assert.Equal(1, normal.TotalGames);
		int expectedWins = actualWinner == PlayerColor.Black ? 1 : 0;
		int expectedLosses = actualWinner == PlayerColor.White ? 1 : 0;
		int expectedDraws = actualWinner == null ? 1 : 0;
		Assert.Equal(expectedWins, normal.Wins);
		Assert.Equal(expectedLosses, normal.Losses);
		Assert.Equal(expectedDraws, normal.Draws);

		// 「最初の有効手を選び続ける」決定的な対局は、対局中に少なくとも 1 回の強制パスを
		// 自然に発生させる（境界ケース: パスが絡む対局のシナリオを兼ねる）。
		Assert.Contains(record.Moves, m => m.IsPass);
	}

	/// <summary>
	/// 対局途中で Undo を挟んでも、その後継続して終局まで到達した際に Winner・棋譜が
	/// 実際の最終盤面と整合していることを確認する（境界ケース: Undo を挟む対局）。
	/// パス条件: Undo 後に対局を再開・完走した結果、KifuPlayer の再生盤面が実際の
	/// 最終盤面と完全一致し、LastKifuRecord.Result / FinalScore も実際の結果と一致すること。
	/// </summary>
	[Fact]
	public async Task FullGame_WithMidGameUndo_StillReachesConsistentEnding()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));

		// 数手進めてから 1 回 Undo する
		for (int i = 0; i < 4 && vm.IsGameInProgress; i++)
		{
			var moveDeadline = DateTime.UtcNow.AddSeconds(5);
			while (vm.BoardSquares.All(sq => !sq.IsValidMove) && DateTime.UtcNow < moveDeadline)
				await Task.Delay(20);

			var validSquare = vm.BoardSquares.FirstOrDefault(sq => sq.IsValidMove);
			if (validSquare == null) break;
			vm.SquareClickedCommand.Execute(validSquare.Position);
			await Task.Delay(50);
		}

		vm.UndoCommand.Execute(null);
		await Task.Delay(50);

		await PlayFullGameAsync(vm);

		Assert.False(vm.IsGameInProgress);

		var actualBoard = vm.EngineCurrentBoard;
		var (actualWinner, actualBlack, actualWhite) = OthelloRules.GetGameResult(actualBoard);

		var record = vm.LastKifuRecord;
		Assert.NotNull(record);
		Assert.Equal(actualWinner, record!.Result);
		Assert.Equal(actualBlack, record.FinalScore.Black);
		Assert.Equal(actualWhite, record.FinalScore.White);

		var player = new KifuPlayer(record);
		player.GoToEnd();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				Assert.Equal(actualBoard.GetPiece(r, c), player.CurrentBoard.GetPiece(r, c));
	}
}
