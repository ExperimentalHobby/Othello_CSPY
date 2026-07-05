using System.Windows.Input;
using Technopro.Othello.Core.Stats;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// 棋力評価・統計情報を右パネルに表示するための ViewModel。
/// GameViewModel が保持し、EndGame() 後に Refresh() を呼び出す。
/// </summary>
public class StatsViewModel : ViewModelBase
{
    private readonly IStatsRepository _repo;
    private Core.Stats.GameStats _stats;

    /// <param name="repo">統計リポジトリ（テスト時はインメモリ実装を注入する）</param>
    public StatsViewModel(IStatsRepository repo)
    {
        _repo = repo;
        _stats = repo.Load();
        ResetCommand = new RelayCommand(OnReset);
    }

    // ── Beginner ─────────────────────────────────────────────────────────────
    /// <summary>Beginner 難易度の勝利数</summary>
    public int BeginnerWins    => _stats.Beginner.Wins;
    /// <summary>Beginner 難易度の敗北数</summary>
    public int BeginnerLosses  => _stats.Beginner.Losses;
    /// <summary>Beginner 難易度の総ゲーム数</summary>
    public int BeginnerTotal   => _stats.Beginner.TotalGames;
    /// <summary>Beginner 難易度の勝率テキスト</summary>
    public string BeginnerWinRate => FormatRate(_stats.Beginner.WinRate);

    // ── Easy ─────────────────────────────────────────────────────────────────
    /// <summary>Easy 難易度の勝利数</summary>
    public int EasyWins    => _stats.Easy.Wins;
    /// <summary>Easy 難易度の敗北数</summary>
    public int EasyLosses  => _stats.Easy.Losses;
    /// <summary>Easy 難易度の総ゲーム数</summary>
    public int EasyTotal   => _stats.Easy.TotalGames;
    /// <summary>Easy 難易度の勝率テキスト（例: "70%"）</summary>
    public string EasyWinRate => FormatRate(_stats.Easy.WinRate);

    // ── Normal ───────────────────────────────────────────────────────────────
    /// <summary>Normal 難易度の勝利数</summary>
    public int NormalWins   => _stats.Normal.Wins;
    /// <summary>Normal 難易度の敗北数</summary>
    public int NormalLosses => _stats.Normal.Losses;
    /// <summary>Normal 難易度の総ゲーム数</summary>
    public int NormalTotal  => _stats.Normal.TotalGames;
    /// <summary>Normal 難易度の勝率テキスト</summary>
    public string NormalWinRate => FormatRate(_stats.Normal.WinRate);

    // ── Hard ─────────────────────────────────────────────────────────────────
    /// <summary>Hard 難易度の勝利数</summary>
    public int HardWins   => _stats.Hard.Wins;
    /// <summary>Hard 難易度の敗北数</summary>
    public int HardLosses => _stats.Hard.Losses;
    /// <summary>Hard 難易度の総ゲーム数</summary>
    public int HardTotal  => _stats.Hard.TotalGames;
    /// <summary>Hard 難易度の勝率テキスト</summary>
    public string HardWinRate => FormatRate(_stats.Hard.WinRate);

    // ── Expert ───────────────────────────────────────────────────────────────
    /// <summary>Expert 難易度の勝利数</summary>
    public int ExpertWins   => _stats.Expert.Wins;
    /// <summary>Expert 難易度の敗北数</summary>
    public int ExpertLosses => _stats.Expert.Losses;
    /// <summary>Expert 難易度の総ゲーム数</summary>
    public int ExpertTotal  => _stats.Expert.TotalGames;
    /// <summary>Expert 難易度の勝率テキスト</summary>
    public string ExpertWinRate => FormatRate(_stats.Expert.WinRate);

    // ── 通算 ─────────────────────────────────────────────────────────────────
    /// <summary>全難易度の総ゲーム数</summary>
    public int TotalGames =>
        _stats.Beginner.TotalGames + _stats.Easy.TotalGames + _stats.Normal.TotalGames
        + _stats.Hard.TotalGames + _stats.Expert.TotalGames;

    /// <summary>現在の連勝数</summary>
    public int CurrentStreak  => _stats.CurrentStreak;

    /// <summary>最大連勝記録</summary>
    public int MaxStreak      => _stats.MaxStreak;

    /// <summary>最高石数差（最大勝利マージン）</summary>
    public int BestWinMargin  => _stats.BestWinMargin;

    /// <summary>統計リセットコマンド</summary>
    public ICommand ResetCommand { get; }

    /// <summary>
    /// リポジトリから統計を再読み込みして全バインディングプロパティを更新する。
    /// GameViewModel の EndGame() から呼び出す。
    /// </summary>
    public void Refresh()
    {
        _stats = _repo.Load();
        // "" を渡すと全プロパティの変更通知が発火する（INotifyPropertyChanged 仕様）
        OnPropertyChanged(string.Empty);
    }

    private void OnReset()
    {
        _repo.Reset();
        Refresh();
    }

    // "P0" 書式指定子はカルチャ（ICU/NLS）によって "%" の前に空白が入るかどうかが変わり、
    // Windows と Linux で表示結果が異なってしまうため、手動組み立てで固定する。
    private static string FormatRate(double rate) => $"{(int)Math.Round(rate * 100)}%";
}
