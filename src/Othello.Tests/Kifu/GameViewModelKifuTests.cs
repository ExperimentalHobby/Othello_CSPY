namespace Technopro.Othello.Tests.Kifu;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.ViewModels;

/// <summary>
/// GameViewModel の棋譜収集テスト。
/// FakeAI を注入し、AI と人間が交互に合法手[0]を打ち続けて終局させ、LastKifuRecord を検証する。
/// </summary>
public class GameViewModelKifuTests
{
	/// <summary>常に最初の合法手を返す決定的なモック AI。</summary>
	private sealed class FakeAI : IAIStrategy
	{
		public DifficultyLevel Difficulty { get; }
		public string EngineName => "AI: Fake";

		public FakeAI(DifficultyLevel d) => Difficulty = d;

		public Position GetBestMove(Board board, PlayerColor playerColor)
			=> OthelloRules.GetValidMoves(board, playerColor)[0];
	}

	/// <summary>
	/// 人間（白・後手）と FakeAI（黒・先手）が交互に合法手[0]を打ち続けて終局させ、
	/// GameViewModel を返す。人間の手番は BoardSquares.IsValidMove が true の
	/// マスを SquareClickedCommand 経由で打つことで自動化する。
	/// </summary>
	private static async Task<GameViewModel> PlayUntilGameOverAsync()
	{
		// human=白(index=1) / AI=黒 (先手) で開始
		var vm = new GameViewModel(d => new FakeAI(d));
		vm.HumanColorIndex = 1;

		// 1 手 ~800ms (AI delay 300+500) × 最大 60 手 ≈ 50 秒。余裕を持って 90 秒タイムアウト
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		var deadline = DateTime.UtcNow.AddSeconds(90);

		while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
		{
			await Task.Delay(30, cts.Token);

			if (vm.IsAIThinking)
				continue;

			// 人間（白）ターン: IsValidMove = true のマスを 1 つクリックする
			var validSq = vm.BoardSquares.FirstOrDefault(sq => sq.IsValidMove);
			if (validSq != null)
				vm.SquareClickedCommand.Execute(validSq.Position);
		}

		return vm;
	}

	// ===== テスト =====

	/// <summary>
	/// ゲーム終了後 LastKifuRecord が非 null であることを確認する。
	/// パス条件: LastKifuRecord != null かつ Version == 1。
	/// </summary>
	[Fact]
	public async Task AfterGameEnd_LastKifuRecord_IsNotNull()
	{
		var vm = await PlayUntilGameOverAsync();
		var kifu = vm.LastKifuRecord;

		Assert.NotNull(kifu);
		Assert.Equal(1, kifu.Version);
	}

	/// <summary>
	/// LastKifuRecord.Moves の件数が 0 より大きいことを確認する。
	/// パス条件: Moves.Count > 0。
	/// </summary>
	[Fact]
	public async Task AfterGameEnd_LastKifuRecord_HasMoves()
	{
		var vm = await PlayUntilGameOverAsync();
		var kifu = vm.LastKifuRecord;

		Assert.NotNull(kifu);
		Assert.True(kifu.Moves.Count > 0, "棋譜に着手が記録されていない");
	}

	/// <summary>
	/// LastKifuRecord.FinalScore の合計が 4 以上 64 以下であることを確認する。
	/// パス条件: 4 &lt;= black + white &lt;= 64。
	/// </summary>
	[Fact]
	public async Task AfterGameEnd_FinalScore_IsValid()
	{
		var vm = await PlayUntilGameOverAsync();
		var kifu = vm.LastKifuRecord;

		Assert.NotNull(kifu);
		int total = kifu.FinalScore.Black + kifu.FinalScore.White;
		Assert.InRange(total, 4, 64);
	}

	/// <summary>
	/// 新規ゲーム開始直後は LastKifuRecord が null にリセットされることを確認する。
	/// パス条件: 終局後は非 null、NewGameCommand 実行直後は null。
	/// </summary>
	[Fact]
	public async Task NewGame_ResetsLastKifuRecord()
	{
		var vm = await PlayUntilGameOverAsync();

		// 終局後は非 null
		Assert.NotNull(vm.LastKifuRecord);

		// 新規ゲーム開始直後は null にリセット
		vm.NewGameCommand.Execute(null);
		Assert.Null(vm.LastKifuRecord);
	}
}
