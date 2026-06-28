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
}
