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
	/// Beginner/Normal/Hard/Expert 各難易度のプロパティが repo.Load() の内容を正しく反映することを確認する
	/// （Issue #196: Easy 難易度のみに偏っていたテストを拡張）。
	/// パス条件: 各難易度の Wins/Losses/Draws/Total/WinRate がリポジトリの値と一致すること。
	/// </summary>
	[Fact]
	public void Constructor_LoadsStatsForAllDifficulties()
	{
		var repo = new InMemoryStatsRepository
		{
			Stats = new GameStats
			{
				Beginner = new DifficultyStats { Wins = 5, Losses = 0, Draws = 0 },
				Normal = new DifficultyStats { Wins = 2, Losses = 2, Draws = 0 },
				Hard = new DifficultyStats { Wins = 1, Losses = 3, Draws = 1 },
				Expert = new DifficultyStats { Wins = 0, Losses = 1, Draws = 0 },
			}
		};

		var vm = new StatsViewModel(repo);

		Assert.Equal(5, vm.BeginnerWins);
		Assert.Equal(0, vm.BeginnerLosses);
		Assert.Equal(0, vm.BeginnerDraws);
		Assert.Equal(5, vm.BeginnerTotal);
		Assert.Equal("100%", vm.BeginnerWinRate);

		Assert.Equal(2, vm.NormalWins);
		Assert.Equal(2, vm.NormalLosses);
		Assert.Equal(0, vm.NormalDraws);
		Assert.Equal(4, vm.NormalTotal);
		Assert.Equal("50%", vm.NormalWinRate);

		Assert.Equal(1, vm.HardWins);
		Assert.Equal(3, vm.HardLosses);
		Assert.Equal(1, vm.HardDraws);
		Assert.Equal(5, vm.HardTotal);
		Assert.Equal("25%", vm.HardWinRate);

		Assert.Equal(0, vm.ExpertWins);
		Assert.Equal(1, vm.ExpertLosses);
		Assert.Equal(0, vm.ExpertDraws);
		Assert.Equal(1, vm.ExpertTotal);
		Assert.Equal("0%", vm.ExpertWinRate);
	}

	/// <summary>
	/// 通算統計（CurrentStreak/MaxStreak/BestWinMargin）が repo.Load() の内容を正しく反映することを確認する
	/// （Issue #196）。
	/// パス条件: 各プロパティがリポジトリの値と一致すること。
	/// </summary>
	[Fact]
	public void Constructor_LoadsOverallStats()
	{
		var repo = new InMemoryStatsRepository
		{
			Stats = new GameStats { CurrentStreak = 3, MaxStreak = 7, BestWinMargin = 40 }
		};

		var vm = new StatsViewModel(repo);

		Assert.Equal(3, vm.CurrentStreak);
		Assert.Equal(7, vm.MaxStreak);
		Assert.Equal(40, vm.BestWinMargin);
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
