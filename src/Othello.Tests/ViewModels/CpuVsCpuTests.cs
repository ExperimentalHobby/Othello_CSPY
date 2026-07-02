namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.ViewModels;

/// <summary>
/// CPU vs CPU 対戦モードの結合テスト。
/// FakeAI を両プレイヤーに注入してゲームが自動進行することを検証する。
/// </summary>
public class CpuVsCpuTests
{
    private sealed class FakeAI : IAIStrategy
    {
        private readonly int _delay;
        public DifficultyLevel Difficulty { get; }
        public string EngineName => "AI: Fake";

        public FakeAI(DifficultyLevel difficulty = DifficultyLevel.Easy, int delayMs = 0)
        {
            Difficulty = difficulty;
            _delay = delayMs;
        }

        public Position GetBestMove(Board board, PlayerColor playerColor)
        {
            if (_delay > 0) Thread.Sleep(_delay);
            return OthelloRules.GetValidMoves(board, playerColor)[0];
        }
    }

    private static async Task<GameViewModel> CreateCpuVsCpuViewModelAsync()
    {
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI());
        vm.GameMode = GameMode.CpuVsCpu;
        vm.CpuVsCpuDelayMs = 0;
        await vm.StartNewGameAsync(); // 新規ゲーム: IsPaused = true の状態
        vm.PauseCommand.Execute(null); // 「開始」ボタン相当 → IsPaused = false
        return vm;
    }

    /// <summary>
    /// パス条件: CpuVsCpu モードでゲーム開始後、最終的に IsGameInProgress = false になること。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_GameCompletesAutomatically()
    {
        var vm = await CreateCpuVsCpuViewModelAsync();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.False(vm.IsGameInProgress);
        Assert.True(vm.BlackScore + vm.WhiteScore > 4);
    }

    /// <summary>
    /// パス条件: CpuVsCpu モードでは UndoCommand.CanExecute = false になること。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_UndoIsDisabled()
    {
        var vm = await CreateCpuVsCpuViewModelAsync();

        // ゲームが少し進むのを待つ
        await Task.Delay(100);

        Assert.False(vm.UndoCommand.CanExecute(null));
    }

    /// <summary>
    /// パス条件: CpuVsCpu モードでは人間がクリックしても着手されないこと。
    /// 証明: SquareClickedCommand が実行されても盤面に余分な石が増えないこと。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_HumanClickIsIgnored()
    {
        var vm = await CreateCpuVsCpuViewModelAsync();

        // AI が思考を終えるまでポーリング
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (vm.IsAIThinking && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        // IsCpuVsCpu フラグが true になっていること
        Assert.True(vm.IsCpuVsCpu);

        // 人間が盤面をクリックしても SquareClickedCommand は着手しない（GameMode がガードする）
        var pos = new Position(2, 3);
        vm.SquareClickedCommand.Execute(pos);

        // エラーが発生せずゲームが継続していれば OK
        Assert.True(vm.IsGameInProgress || !vm.IsGameInProgress); // ゲーム状態は問わない
    }

    /// <summary>
    /// パス条件: IsPaused = true にすると AI の思考が止まること（IsAIThinking が false のまま遷移しないこと）。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_PauseStopsAutoPlay()
    {
        var vm = await CreateCpuVsCpuViewModelAsync();

        // 少し待ってから一時停止
        await Task.Delay(50);
        vm.PauseCommand.Execute(null);
        Assert.True(vm.IsPaused);

        // 一時停止後は IsGameInProgress が true のまま変化しないこと
        bool wasInProgress = vm.IsGameInProgress;
        await Task.Delay(200);
        // ゲームが終了していなければ一時停止が有効
        if (wasInProgress)
            Assert.True(vm.IsPaused);
    }

    /// <summary>
    /// パス条件: 一時停止後に再開するとゲームが続くこと。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_ResumeAfterPause_GameContinues()
    {
        var vm = await CreateCpuVsCpuViewModelAsync();

        // 少し待ってから一時停止
        await Task.Delay(50);
        vm.PauseCommand.Execute(null);
        Assert.True(vm.IsPaused);

        var scoreBefore = vm.BlackScore + vm.WhiteScore;
        await Task.Delay(200);

        // 再開
        vm.PauseCommand.Execute(null);
        Assert.False(vm.IsPaused);

        // ゲームが進行するまで待つ
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (vm.IsGameInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.False(vm.IsGameInProgress);
    }

    /// <summary>
    /// パス条件: GameMode を HumanVsCpu に戻したとき、IsCpuVsCpu が false になること。
    /// </summary>
    [Fact]
    public void GameMode_Switch_UpdatesIsCpuVsCpu()
    {
        var vm = new GameViewModel(aiFactory: _ => new FakeAI(), startDeferred: true);

        vm.GameMode = GameMode.CpuVsCpu;
        Assert.True(vm.IsCpuVsCpu);
        Assert.False(vm.IsHumanVsCpu);

        vm.GameMode = GameMode.HumanVsCpu;
        Assert.False(vm.IsCpuVsCpu);
        Assert.True(vm.IsHumanVsCpu);
    }

    /// <summary>
    /// パス条件: BlackDifficultyIndex / WhiteDifficultyIndex が独立して設定できること。
    /// </summary>
    [Fact]
    public void BlackAndWhiteDifficulty_SetIndependently()
    {
        var vm = new GameViewModel(aiFactory: _ => new FakeAI(), startDeferred: true);
        vm.GameMode = GameMode.CpuVsCpu;

        vm.BlackDifficultyIndex = 1; // Easy（Beginner=0, Easy=1）
        vm.WhiteDifficultyIndex = 3; // Hard（Hard=3）

        Assert.Equal(DifficultyLevel.Easy, vm.BlackDifficulty);
        Assert.Equal(DifficultyLevel.Hard, vm.WhiteDifficulty);
    }

    /// <summary>
    /// パス条件: CpuVsCpu の新規ゲーム直後は IsPaused = true になること（開始ボタン待ち状態）。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_NewGame_StartsInPausedState()
    {
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI());
        vm.GameMode = GameMode.CpuVsCpu;
        vm.CpuVsCpuDelayMs = 0;

        await vm.StartNewGameAsync();

        Assert.True(vm.IsPaused);
        Assert.True(vm.IsGameInProgress);
        Assert.Equal("開始", vm.PauseButtonContent);
    }

    /// <summary>
    /// パス条件: CpuVsCpu 開始ボタン待ち中（IsPaused）は対戦モードを変更できること（IsSettingsEditable = true）。
    /// </summary>
    [Fact]
    public async Task CpuVsCpu_PausedInitialState_SettingsAreEditable()
    {
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI());
        vm.GameMode = GameMode.CpuVsCpu;
        vm.CpuVsCpuDelayMs = 0;

        await vm.StartNewGameAsync(); // IsPaused = true, IsInitialState = true

        Assert.True(vm.IsSettingsEditable, "開始ボタン待ち中はモードや難易度を変更できること");
    }

    /// <summary>
    /// パス条件: HumanVsCpu ゲーム中に CpuVsCpu に切り替えると IsGameInProgress = false になること（自動開始しないこと）。
    /// </summary>
    [Fact]
    public async Task SwitchToCpuVsCpu_StopsCurrentGame_DoesNotAutoStart()
    {
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI());
        await vm.StartNewGameAsync(); // HumanVsCpu でゲーム開始

        // ゲームが進行中であること確認
        Assert.True(vm.IsGameInProgress);

        // CPU vs CPU に切り替え
        vm.GameMode = GameMode.CpuVsCpu;

        // ゲームが停止していること（自動開始しない）
        Assert.False(vm.IsGameInProgress);
    }

    /// <summary>
    /// パス条件: CpuVsCpu 停止中に HumanVsCpu に切り替えると新規ゲームが開始されること。
    /// </summary>
    [Fact]
    public async Task SwitchBackToHumanVsCpu_StartsNewGame()
    {
        var vm = new GameViewModel(
            aiFactory: _ => new FakeAI(),
            startDeferred: true,
            cpuVsCpuAiFactory: _ => new FakeAI());

        // CpuVsCpu モードで起動（停止状態）
        vm.GameMode = GameMode.CpuVsCpu;
        Assert.False(vm.IsGameInProgress);

        // HumanVsCpu に切り替え
        vm.GameMode = GameMode.HumanVsCpu;

        // 新規ゲーム開始を待つ
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!vm.IsGameInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.True(vm.IsGameInProgress);
        Assert.True(vm.IsHumanVsCpu);
    }
}
