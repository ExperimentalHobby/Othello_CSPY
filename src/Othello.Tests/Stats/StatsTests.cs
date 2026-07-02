namespace Technopro.Othello.Tests.Stats;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Stats;
using Technopro.Othello.ViewModels;

/// <summary>
/// 棋力評価・統計機能のテスト。
/// GameStats モデル・StatsRepository・GameViewModel 統合を網羅する。
/// </summary>
public class StatsTests
{
    // ─── DifficultyStats ─────────────────────────────────────────────────────

    /// <summary>パス条件: ゲーム 0 件のとき WinRate = 0.0 であること。</summary>
    [Fact]
    public void DifficultyStats_WinRate_IsZero_WhenNoGames()
    {
        var s = new DifficultyStats();
        Assert.Equal(0.0, s.WinRate);
    }

    /// <summary>パス条件: 全勝のとき WinRate = 1.0 であること。</summary>
    [Fact]
    public void DifficultyStats_WinRate_IsOne_WhenAllWins()
    {
        var s = new DifficultyStats { Wins = 5, Losses = 0 };
        Assert.Equal(1.0, s.WinRate);
    }

    /// <summary>パス条件: 勝敗同数のとき WinRate = 0.5 であること。</summary>
    [Fact]
    public void DifficultyStats_WinRate_IsHalf_WhenEqualWinsAndLosses()
    {
        var s = new DifficultyStats { Wins = 3, Losses = 3 };
        Assert.Equal(0.5, s.WinRate);
    }

    /// <summary>パス条件: 引き分けは WinRate の分母に含まれないこと。</summary>
    [Fact]
    public void DifficultyStats_WinRate_ExcludesDraws()
    {
        // 勝1・負1・引き分け100 → WinRate = 0.5（引き分けは無視）
        var s = new DifficultyStats { Wins = 1, Losses = 1, Draws = 100 };
        Assert.Equal(0.5, s.WinRate);
    }

    /// <summary>パス条件: TotalGames = 0 のとき AverageMoves = 0.0 であること。</summary>
    [Fact]
    public void DifficultyStats_AverageMoves_IsZero_WhenNoGames()
    {
        var s = new DifficultyStats();
        Assert.Equal(0.0, s.AverageMoves);
    }

    /// <summary>パス条件: TotalMoves / TotalGames が正しく計算されること。</summary>
    [Fact]
    public void DifficultyStats_AverageMoves_CalculatesCorrectly()
    {
        var s = new DifficultyStats { Wins = 2, TotalMoves = 100 };
        Assert.Equal(50.0, s.AverageMoves);
    }

    // ─── GameStats.RecordResult ───────────────────────────────────────────────

    /// <summary>パス条件: 勝利後に Wins が 1 増え、連勝数が 1 増えること。</summary>
    [Fact]
    public void GameStats_RecordResult_Win_IncreasesWinsAndStreak()
    {
        var gs = new GameStats();
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 10);

        Assert.Equal(1, gs.Normal.Wins);
        Assert.Equal(0, gs.Normal.Losses);
        Assert.Equal(1, gs.CurrentStreak);
    }

    /// <summary>パス条件: 敗北後に Losses が 1 増え、連勝数がリセットされること。</summary>
    [Fact]
    public void GameStats_RecordResult_Loss_IncreasesLossesAndResetsStreak()
    {
        var gs = new GameStats { CurrentStreak = 3 };
        gs.RecordResult(PlayerColor.White, PlayerColor.Black, DifficultyLevel.Easy, 40, 0);

        Assert.Equal(1, gs.Easy.Losses);
        Assert.Equal(0, gs.CurrentStreak);
    }

    /// <summary>パス条件: 引き分け後に Draws が 1 増え、連勝数がリセットされること。</summary>
    [Fact]
    public void GameStats_RecordResult_Draw_IncreasesDrawsAndResetsStreak()
    {
        var gs = new GameStats { CurrentStreak = 2 };
        gs.RecordResult(null, PlayerColor.Black, DifficultyLevel.Hard, 60, 0);

        Assert.Equal(1, gs.Hard.Draws);
        Assert.Equal(0, gs.CurrentStreak);
    }

    /// <summary>パス条件: 連勝が続くと MaxStreak が更新されること。</summary>
    [Fact]
    public void GameStats_RecordResult_Win_UpdatesMaxStreak()
    {
        var gs = new GameStats();
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 5);
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 5);
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 5);

        Assert.Equal(3, gs.MaxStreak);
        Assert.Equal(3, gs.CurrentStreak);
    }

    /// <summary>パス条件: 敗北後も MaxStreak は維持され、CurrentStreak のみリセットされること。</summary>
    [Fact]
    public void GameStats_RecordResult_Loss_PreservesMaxStreak()
    {
        var gs = new GameStats();
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 5);
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 40, 5);
        gs.RecordResult(PlayerColor.White, PlayerColor.Black, DifficultyLevel.Medium, 40, 0); // 敗北

        Assert.Equal(2, gs.MaxStreak);
        Assert.Equal(0, gs.CurrentStreak);
    }

    /// <summary>パス条件: 勝利時に BestWinMargin が更新されること。</summary>
    [Fact]
    public void GameStats_RecordResult_Win_UpdatesBestWinMargin()
    {
        var gs = new GameStats();
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Easy, 50, 20);
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Easy, 50, 42);
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Easy, 50, 10);

        Assert.Equal(42, gs.BestWinMargin);
    }

    /// <summary>パス条件: TotalMoves が累積されること。</summary>
    [Fact]
    public void GameStats_RecordResult_AccumulatesTotalMoves()
    {
        var gs = new GameStats();
        gs.RecordResult(PlayerColor.Black, PlayerColor.Black, DifficultyLevel.Medium, 55, 0);
        gs.RecordResult(PlayerColor.White, PlayerColor.Black, DifficultyLevel.Medium, 45, 0);

        Assert.Equal(100, gs.Normal.TotalMoves);
    }

    // ─── StatsRepository ─────────────────────────────────────────────────────

    /// <summary>パス条件: ファイルが存在しないとき Load() が空の GameStats を返すこと。</summary>
    [Fact]
    public void StatsRepository_Load_ReturnsDefault_WhenFileNotExists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stats_test_{Guid.NewGuid()}.json");
        var repo = new StatsRepository(path);

        var stats = repo.Load();

        Assert.Equal(0, stats.Normal.Wins);
        Assert.Equal(0, stats.CurrentStreak);
    }

    /// <summary>パス条件: Save() → Load() でデータが正確に往復すること。</summary>
    [Fact]
    public void StatsRepository_SaveAndLoad_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stats_test_{Guid.NewGuid()}.json");
        var repo = new StatsRepository(path);
        try
        {
            var original = new GameStats { CurrentStreak = 3, MaxStreak = 5 };
            original.Easy.Wins = 7;
            original.Normal.Losses = 2;
            original.BestWinMargin = 30;

            repo.Save(original);
            var loaded = repo.Load();

            Assert.Equal(7, loaded.Easy.Wins);
            Assert.Equal(2, loaded.Normal.Losses);
            Assert.Equal(3, loaded.CurrentStreak);
            Assert.Equal(5, loaded.MaxStreak);
            Assert.Equal(30, loaded.BestWinMargin);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>パス条件: Reset() 後に Load() が空の GameStats を返すこと。</summary>
    [Fact]
    public void StatsRepository_Reset_ClearsStats()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stats_test_{Guid.NewGuid()}.json");
        var repo = new StatsRepository(path);
        try
        {
            var gs = new GameStats { CurrentStreak = 10 };
            gs.Normal.Wins = 5;
            repo.Save(gs);

            repo.Reset();
            var loaded = repo.Load();

            Assert.Equal(0, loaded.Normal.Wins);
            Assert.Equal(0, loaded.CurrentStreak);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ─── GameViewModel 統合 ───────────────────────────────────────────────────

    private sealed class FakeAI : IAIStrategy
    {
        public DifficultyLevel Difficulty => DifficultyLevel.Easy;
        public string EngineName => "Fake";
        public Position GetBestMove(Board board, PlayerColor playerColor)
            => OthelloRules.GetValidMoves(board, playerColor)[0];
    }

    private sealed class InMemoryStatsRepository : IStatsRepository
    {
        private GameStats _stats = new();
        public GameStats Load() => _stats;
        public void Save(GameStats stats) => _stats = stats;
        public void Reset() => _stats = new();
    }

    /// <summary>
    /// パス条件: HumanVsCpu ゲームが終了すると Stats.TotalGames が増えること。
    /// </summary>
    [Fact]
    public async Task GameViewModel_HumanVsCpu_EndGame_RecordsStats()
    {
        var statsRepo = new InMemoryStatsRepository();
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            statsRepository: statsRepo);

        await vm.StartNewGameAsync();

        // 人間が全マスに着手してゲームを終わらせることは複雑なため、
        // LoadStateForTest で終局局面をセットして EndGame を誘発する
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.Black);
        vm.LoadStateForTest(board, PlayerColor.White);

        // ゲーム終了を待つ（AI が手を打てず終局）
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal(1, vm.Stats.TotalGames);
    }

    /// <summary>
    /// パス条件: CpuVsCpu モードのゲーム終了では Stats.TotalGames が増えないこと。
    /// </summary>
    [Fact]
    public async Task GameViewModel_CpuVsCpu_EndGame_DoesNotRecordStats()
    {
        var statsRepo = new InMemoryStatsRepository();
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI(),
            statsRepository: statsRepo);
        vm.GameMode = GameMode.CpuVsCpu;
        vm.CpuVsCpuDelayMs = 0;
        await vm.StartNewGameAsync();
        vm.PauseCommand.Execute(null); // 開始

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Equal(0, vm.Stats.TotalGames);
    }
}
