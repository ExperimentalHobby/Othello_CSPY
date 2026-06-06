namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.ViewModels;

/// <summary>
/// GameViewModel の結合テスト。
/// 実 Python AI の代わりにモック AI を注入し、AI 連携・設定変更・キャンセルの挙動を検証する。
/// </summary>
public class GameViewModelTests
{
    /// <summary>
    /// 盤面から常に最初の有効手を返す決定的なモック AI。
    /// GetBestMove 呼び出し時に onMove コールバックを発火する（AI が着手したことの検出に使う）。
    /// </summary>
    private sealed class FakeAI : IAIStrategy
    {
        private readonly Action? _onMove;
        public DifficultyLevel Difficulty { get; }

        public FakeAI(DifficultyLevel difficulty, Action? onMove = null)
        {
            Difficulty = difficulty;
            _onMove = onMove;
        }

        public Position GetBestMove(Board board, PlayerColor playerColor)
        {
            var move = OthelloRules.GetValidMoves(board, playerColor)[0];
            _onMove?.Invoke();
            return move;
        }
    }

    /// <summary>
    /// GetBestMove が解放されるまでブロックし、Dispose されると例外を投げるモック AI。
    /// 「AI 思考中に新規ゲーム」の競合（プロセス強制終了に相当）を再現するために使う。
    /// </summary>
    private sealed class BlockingFakeAI : IAIStrategy, IDisposable
    {
        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _gate = new(false);
        private volatile bool _disposed;
        public DifficultyLevel Difficulty { get; }

        public BlockingFakeAI(DifficultyLevel difficulty, ManualResetEventSlim entered)
        {
            Difficulty = difficulty;
            _entered = entered;
        }

        public Position GetBestMove(Board board, PlayerColor playerColor)
        {
            _entered.Set();   // 思考開始を通知
            _gate.Wait();     // 解放 or Dispose まで待機
            if (_disposed)
                throw new InvalidOperationException("AI disposed (プロセス強制終了に相当)");
            return OthelloRules.GetValidMoves(board, playerColor)[0];
        }

        public void Dispose()
        {
            _disposed = true;
            _gate.Set();
        }
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 注入した AI ファクトリで構築すると、盤面 64 マス・初期スコア 2/2・進行中状態になることを確認する。
    /// パス条件: BoardSquares が 64、BlackScore/WhiteScore が 2、IsGameInProgress が true、ファクトリ呼び出しが 1 回であること。
    /// </summary>
    [Fact]
    public void Constructor_WithInjectedFactory_InitializesGame()
    {
        int created = 0;
        using var vm = new GameViewModel(d => { created++; return new FakeAI(d); });

        Assert.True(vm.IsGameInProgress);
        Assert.Equal(64, vm.BoardSquares.Count);
        Assert.Equal(2, vm.BlackScore);
        Assert.Equal(2, vm.WhiteScore);
        Assert.Equal(1, created); // 人間=黒（先手）なので AI は初手を打たず、ファクトリ生成は 1 回のみ
    }

    /// <summary>
    /// DifficultyIndex / HumanColorIndex が DifficultyLevel / PlayerColor と正しく相互変換されることを確認する。
    /// パス条件: 各 index 設定後に対応する列挙値が得られ、getter も一致すること。
    /// </summary>
    [Fact]
    public void IndexProperties_MapToEnums()
    {
        using var vm = new GameViewModel(d => new FakeAI(d));

        vm.DifficultyIndex = 0;
        Assert.Equal(DifficultyLevel.Easy, vm.Difficulty);
        vm.DifficultyIndex = 2;
        Assert.Equal(DifficultyLevel.Hard, vm.Difficulty);
        Assert.Equal(2, vm.DifficultyIndex);

        vm.HumanColorIndex = 0;
        Assert.Equal(PlayerColor.Black, vm.HumanColor);
        Assert.Equal(0, vm.HumanColorIndex);
    }

    /// <summary>
    /// 初手前に難易度を変更すると、新しい難易度で AI を作り直してゲームが再起動することを確認する（#3）。
    /// パス条件: 難易度変更後にファクトリが再度呼ばれ、生成された AI の Difficulty が新しい値であること。
    /// </summary>
    [Fact]
    public void ChangingDifficulty_BeforeFirstMove_RestartsWithNewDifficulty()
    {
        int created = 0;
        FakeAI? last = null;
        using var vm = new GameViewModel(d => { created++; last = new FakeAI(d); return last; });

        Assert.Equal(1, created);

        vm.DifficultyIndex = 2; // Hard へ変更 → 初期状態なので再起動

        Assert.Equal(2, created);                              // AI を作り直した
        Assert.Equal(DifficultyLevel.Hard, last!.Difficulty); // 新しい難易度が反映されている
        Assert.True(vm.IsGameInProgress);
    }

    /// <summary>
    /// 初手前に色を白へ変更すると再起動し、AI（黒）が先手として実際に着手することを確認する（#3 ソフトロック回帰）。
    /// パス条件: 色変更後、AI の GetBestMove が呼ばれること（手番不整合で固まらない）。
    /// </summary>
    [Fact]
    public void ChangingColorToWhite_BeforeFirstMove_LetsAiTakeFirstTurn()
    {
        using var aiMoved = new ManualResetEventSlim(false);
        using var vm = new GameViewModel(d => new FakeAI(d, () => aiMoved.Set()));

        vm.HumanColorIndex = 1; // 白に変更 → 再起動 → AI（黒）が先手

        Assert.True(aiMoved.Wait(Timeout)); // AI が手番を取れた（ソフトロックしない）
        Assert.True(vm.IsGameInProgress);
    }

    /// <summary>
    /// 人間が着手すると AI が応答着手することを確認する（注入 AI 経由の通常フロー）。
    /// パス条件: 人間の着手後に AI の GetBestMove が呼ばれ、ゲームが進行中のままであること。
    /// </summary>
    [Fact]
    public void HumanMove_TriggersAiResponse()
    {
        using var aiMoved = new ManualResetEventSlim(false);
        using var vm = new GameViewModel(d => new FakeAI(d, () => aiMoved.Set()));

        // 人間=黒の初期有効手のひとつに着手する
        vm.SquareClickedCommand.Execute(new Position(2, 3));

        Assert.True(aiMoved.Wait(Timeout)); // AI（白）が応答した
        Assert.True(vm.IsGameInProgress);
    }

    /// <summary>
    /// AI 思考中に新規ゲームを開始しても、古いタスクの例外が新しいゲームを壊さないことを確認する（#2 回帰）。
    /// パス条件: 古い AI の強制終了後も IsGameInProgress が true のままであること。
    /// </summary>
    [Fact]
    public void StartingNewGame_WhileAiThinking_DoesNotBreakNewGame()
    {
        using var entered = new ManualResetEventSlim(false);
        var f = (DifficultyLevel d) => (IAIStrategy)new BlockingFakeAI(d, entered);
        using var vm = new GameViewModel(f);

        // 色を白に変更 → 再起動 → AI（黒）が先手で思考開始しブロックする
        vm.HumanColorIndex = 1;
        Assert.True(entered.Wait(Timeout)); // AI が GetBestMove 内で思考中（ブロック中）

        // 思考中に新規ゲーム開始（旧 cts をキャンセルし旧 AI を Dispose → GetBestMove が例外送出）
        vm.StartNewGame();

        // 旧タスクの catch が走る猶予を与える（修正前はここで IsGameInProgress=false にしていた）
        Thread.Sleep(500);

        Assert.True(vm.IsGameInProgress); // 新しいゲームは壊れていない
    }
}
