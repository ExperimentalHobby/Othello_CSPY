namespace Technopro.Othello.Tests.Stats;

using System.ComponentModel;
using Technopro.Othello.Core.Stats;
using Technopro.Othello.ViewModels;

/// <summary>StatsViewModel の単体テスト。</summary>
public class StatsViewModelTests
{
	private sealed class InMemoryStatsRepository : IStatsRepository
	{
		public GameStats Stats = new();
		public bool ResetCalled;

		public GameStats Load() => Stats;
		public void Save(GameStats stats) => Stats = stats;
		public void Reset()
		{
			ResetCalled = true;
			Stats = new GameStats();
		}
	}

	/// <summary>
	/// コンストラクタが repo.Load() の内容を各難易度プロパティへ反映することを確認する。
	/// パス条件: EasyWins/EasyLosses/EasyTotal/EasyWinRate がリポジトリの値と一致すること。
	/// </summary>
	[Fact]
	public void Constructor_LoadsStatsFromRepository()
	{
		var repo = new InMemoryStatsRepository
		{
			Stats = new GameStats { Easy = new DifficultyStats { Wins = 3, Losses = 1 } }
		};

		var vm = new StatsViewModel(repo);

		Assert.Equal(3, vm.EasyWins);
		Assert.Equal(1, vm.EasyLosses);
		Assert.Equal(4, vm.EasyTotal);
		Assert.Equal("75%", vm.EasyWinRate);
	}

	/// <summary>
	/// 「引」列に TotalGames ではなく Draws 単体が表示されることを確認する（Issue #73）。
	/// パス条件: EasyDraws = 1（Draws のみ）、EasyTotal = 3（勝+負+引の合計）となり、
	/// 両者が異なる値になること。
	/// </summary>
	[Fact]
	public void Draws_OneWinOneLossOneDraw_ReturnsDrawCountNotTotalGames()
	{
		var repo = new InMemoryStatsRepository
		{
			Stats = new GameStats { Easy = new DifficultyStats { Wins = 1, Losses = 1, Draws = 1 } }
		};

		var vm = new StatsViewModel(repo);

		Assert.Equal(1, vm.EasyWins);
		Assert.Equal(1, vm.EasyLosses);
		Assert.Equal(1, vm.EasyDraws);
		Assert.Equal(3, vm.EasyTotal);
	}

	/// <summary>
	/// 勝敗0件の難易度では WinRate が "0%" を返すことを確認する。
	/// パス条件: HardWinRate が "0%" であること。
	/// </summary>
	[Fact]
	public void WinRate_NoGamesPlayed_ReturnsZeroPercent()
	{
		var repo = new InMemoryStatsRepository();

		var vm = new StatsViewModel(repo);

		Assert.Equal("0%", vm.HardWinRate);
	}

	/// <summary>
	/// ResetCommand を実行すると repo.Reset() が呼ばれ、プロパティが初期状態に戻ることを確認する。
	/// パス条件: ResetCalled が true、かつ EasyWins が 0 に戻ること。
	/// </summary>
	[Fact]
	public void ResetCommand_Execute_ResetsRepositoryAndProperties()
	{
		var repo = new InMemoryStatsRepository
		{
			Stats = new GameStats { Easy = new DifficultyStats { Wins = 3, Losses = 1 } }
		};
		var vm = new StatsViewModel(repo);

		vm.ResetCommand.Execute(null);

		Assert.True(repo.ResetCalled);
		Assert.Equal(0, vm.EasyWins);
	}

	/// <summary>
	/// ResetCommand の実行時に PropertyChanged（空文字列 = 全体通知）が発火することを確認する。
	/// パス条件: PropertyChanged イベントが発火し、PropertyName が空文字列であること。
	/// </summary>
	[Fact]
	public void ResetCommand_Execute_RaisesPropertyChanged()
	{
		var repo = new InMemoryStatsRepository();
		var vm = new StatsViewModel(repo);

		PropertyChangedEventArgs? raised = null;
		vm.PropertyChanged += (_, e) => raised = e;

		vm.ResetCommand.Execute(null);

		Assert.NotNull(raised);
		Assert.Equal(string.Empty, raised!.PropertyName);
	}
}
