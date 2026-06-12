using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// オセロゲーム全体を制御する主要 ViewModel。
/// GameEngine（ゲームロジック）と PythonSubprocessAI（AI）を保持し、
/// UI へのデータバインディングを通じてゲームの状態を表示する。
/// WPF・WinUI3 両方から参照される共有 ViewModel。
/// </summary>
public class GameViewModel : ViewModelBase, IDisposable
{
    /// <summary>AI 着手前に挿入する演出用待機時間（ミリ秒）。</summary>
    private const int AiMoveDelayMs = 300;
    /// <summary>AI の連続手番（自分のパス後など）の前に挿入する演出用待機時間（ミリ秒）。</summary>
    private const int AiTurnDelayMs = 500;
    /// <summary>石の反転アニメーション全体の所要時間（ミリ秒）。IsBeingFlipped が True を維持する時間。</summary>
    private const int FlipAnimationDurationMs = 300;

    private readonly GameEngine _engine = new();

    /// <summary>難易度から AI 実装を生成するファクトリ（テスト時はモックを注入できる）。</summary>
    private readonly Func<DifficultyLevel, IAIStrategy> _aiFactory;

    /// <summary>現在のゲームで使用している AI（IDisposable なら破棄時に Dispose する）。</summary>
    private IAIStrategy? _ai;
    private CancellationTokenSource? _cts;

    // --- バッキングフィールド ---
    private int    _blackScore;
    private int    _whiteScore;
    private string _currentPlayerDisplay = "黒のターン";
    private string _statusMessage        = "ゲーム開始";
    private DifficultyLevel _difficulty  = DifficultyLevel.Medium;
    private bool   _isGameInProgress;
    private bool   _isAIThinking;
    private PlayerColor _humanColor = PlayerColor.Black;

    // Undo は RaiseCanExecuteChanged を呼べるよう RelayCommand 型で保持する
    private readonly RelayCommand _undoCommand;

    public ObservableCollection<BoardSquareViewModel> BoardSquares { get; } = new();

    private string _aiEngineLabel = "AI: ?";
    /// <summary>現在使用中の AI バックエンド名（メニューバーに表示）。</summary>
    public string AiEngineLabel
    {
        get => _aiEngineLabel;
        private set => SetProperty(ref _aiEngineLabel, value);
    }

    public int BlackScore { get => _blackScore; set => SetProperty(ref _blackScore, value); }
    public int WhiteScore { get => _whiteScore; set => SetProperty(ref _whiteScore, value); }
    public string CurrentPlayerDisplay { get => _currentPlayerDisplay; set => SetProperty(ref _currentPlayerDisplay, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public DifficultyLevel Difficulty
    {
        get => _difficulty;
        // 初手前（設定変更可能な間）に難易度を変えたら、新しい難易度で AI を作り直すため再起動する
        set { if (SetProperty(ref _difficulty, value)) RestartIfConfiguringBeforeFirstMove(); }
    }

    public bool IsGameInProgress
    {
        get => _isGameInProgress;
        set
        {
            if (SetProperty(ref _isGameInProgress, value))
                OnPropertyChanged(nameof(IsSettingsEditable));
        }
    }

    // 未着手（初期盤面）の間は設定変更を許可する
    public bool IsSettingsEditable => !IsGameInProgress || _engine.IsInitialState;

    public bool IsAIThinking { get => _isAIThinking; set => SetProperty(ref _isAIThinking, value); }

    public PlayerColor HumanColor
    {
        get => _humanColor;
        // 初手前に色を変えたら、手番割り当てが整合するようゲームを再起動する
        // （再起動しないと CurrentPlayer と HumanColor が食い違いソフトロックする）
        set { if (SetProperty(ref _humanColor, value)) RestartIfConfiguringBeforeFirstMove(); }
    }

    /// <summary>
    /// 進行中かつ初手前（IsInitialState）であれば、現在の設定でゲームを再起動する。
    /// 難易度・色の変更を即座に反映し、設定と実ゲーム状態の不整合を防ぐ。
    /// </summary>
    private void RestartIfConfiguringBeforeFirstMove()
    {
        if (IsGameInProgress && _engine.IsInitialState)
            _ = StartNewGameAsync();
    }

    public int DifficultyIndex
    {
        get => (int)Difficulty - 1;
        set => Difficulty = (DifficultyLevel)(value + 1);
    }

    public int HumanColorIndex
    {
        get => HumanColor == PlayerColor.Black ? 0 : 1;
        set => HumanColor = value == 0 ? PlayerColor.Black : PlayerColor.White;
    }

    private PlayerColor AiColor => HumanColor.Opponent();

    public ICommand NewGameCommand { get; }
    public ICommand SquareClickedCommand { get; }
    public ICommand UndoCommand => _undoCommand;

    /// <summary>
    /// 既定のコンストラクタ。実際の Python AI を使用する。
    /// ゲーム開始は呼び出し元（WPF/WinUI3 の Loaded イベント）が StartNewGameAsync() で行う。
    /// </summary>
    public GameViewModel() : this(null, startDeferred: true) { }

    /// <summary>
    /// AI ファクトリを注入できるコンストラクタ（テスト用にモック AI を差し込める）。
    /// </summary>
    /// <param name="aiFactory">難易度から IAIStrategy を生成するファクトリ。null の場合は Python AI を使う。</param>
    /// <param name="startDeferred">true のとき StartNewGame() をコンストラクタから呼ばない（View 側の Loaded イベントで呼び出す）。</param>
    public GameViewModel(Func<DifficultyLevel, IAIStrategy>? aiFactory, bool startDeferred = false)
    {
        _aiFactory = aiFactory ?? CreateDefaultAI;

        NewGameCommand       = new RelayCommand(() => _ = StartNewGameAsync());
        SquareClickedCommand = new RelayCommand<Position>(SquareClicked);

        _undoCommand = new RelayCommand(OnUndo, () =>
            _engine.GameState.IsGameInProgress() && !IsAIThinking);

        InitializeBoard();

        if (!startDeferred)
            StartNewGame();
    }

    /// <summary>
    /// 既定の AI 実装を生成する。Python サブプロセスを優先し、起動に失敗した場合は C# AI にフォールバックする。
    /// Task.Run 内（バックグラウンドスレッド）から呼ばれる可能性があるため、
    /// UI バインド済みプロパティ（AiEngineLabel 等）を更新しない。
    /// ラベル更新は ApplyNewGameState() で UI スレッド側が行う。
    /// </summary>
    private IAIStrategy CreateDefaultAI(DifficultyLevel difficulty)
    {
        try
        {
            return new PythonSubprocessAI(difficulty, AiScriptPaths.AiScriptPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Python AI 起動失敗（C# AI で代替）: {ex.Message}");
            return new AlphaBetaAI(difficulty);
        }
    }

    /// <summary>
    /// IsAIThinking / IsGameInProgress 変更時に Undo の CanExecute を再評価する。
    /// WPF では CommandManager が担うが WinUI3 では手動通知が必要。
    /// </summary>
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(IsAIThinking) or nameof(IsGameInProgress))
            _undoCommand?.RaiseCanExecuteChanged();
    }

    private void InitializeBoard()
    {
        BoardSquares.Clear();
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                BoardSquares.Add(new BoardSquareViewModel(new Position(row, col)));
    }

    /// <summary>
    /// ゲームを同期的に開始する（テスト・Console 用）。
    /// UI スレッドから呼ぶと FindPythonExecutable() でブロックする可能性がある。
    /// GUI アプリでは StartNewGameAsync() を使うこと。
    /// </summary>
    public void StartNewGame()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsAIThinking = false;

        (_ai as IDisposable)?.Dispose();
        _ai = null;

        try
        {
            _ai = _aiFactory(Difficulty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI の起動に失敗しました: {ex.Message}";
            IsGameInProgress = false;
            return;
        }

        ApplyNewGameState();
    }

    /// <summary>
    /// ゲームを非同期で開始する。AI ファクトリ呼び出しをバックグラウンドスレッドで実行し
    /// UI スレッドのブロックを防ぐ（WPF/WinUI3 の Loaded イベントから呼び出す）。
    /// </summary>
    public async Task StartNewGameAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsAIThinking = false;

        (_ai as IDisposable)?.Dispose();
        _ai = null;

        try
        {
            _ai = await Task.Run(() => _aiFactory(Difficulty));
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI の起動に失敗しました: {ex.Message}";
            IsGameInProgress = false;
            return;
        }

        ApplyNewGameState();
    }

    /// <summary>
    /// AI 生成後の共通初期化処理（StartNewGame / StartNewGameAsync 両方から呼ばれる）。
    /// 常に UI スレッドから呼ばれる前提。AiEngineLabel の更新もここで行う。
    /// </summary>
    private void ApplyNewGameState()
    {
        // AI の実装型からバックエンド名を決定（UI スレッドで実行されるため安全）
        AiEngineLabel = _ai switch
        {
            PythonSubprocessAI => AiScriptPaths.IsRustAvailable ? "AI: Rust" : "AI: Python",
            AlphaBetaAI        => "AI: C#",
            _                  => "AI: ?"
        };

        _engine.Initialize();
        OnPropertyChanged(nameof(IsSettingsEditable));
        RefreshBoardDisplay();
        IsGameInProgress = true;

        string humanStr = HumanColor.ToDisplayString();
        string aiStr    = AiColor.ToDisplayString();
        StatusMessage = $"ゲーム開始 - あなた: {humanStr}, AI: {aiStr}";
        UpdateScoreBoardState();

        if (AiColor == PlayerColor.Black)
            _ = ProcessAIMoveAsync(_cts!.Token);
    }

    private void SquareClicked(Position pos)
    {
        if (!IsGameInProgress || IsAIThinking)
            return;

        if (_engine.CurrentPlayer != HumanColor)
            return;

        HandlePlayerMove(pos);
    }

    private void HandlePlayerMove(Position position)
    {
        var result = _engine.MakeMove(position);
        if (!result.IsSuccess)
        {
            StatusMessage = result.Message;
            return;
        }

        _ = AnimateFlipsAsync(result.FlippedPieces);
        NotifyIfPassed();
        OnPropertyChanged(nameof(IsSettingsEditable));
        RefreshBoardDisplay();
        _ = CheckAndProcessNextTurnAsync(_cts!.Token);
    }

    private async Task CheckAndProcessNextTurnAsync(CancellationToken ct)
    {
        try
        {
            UpdateScoreBoardState();

            if (!_engine.GameState.IsGameInProgress())
            {
                EndGame();
                return;
            }

            if (_engine.CurrentPlayer == AiColor)
            {
                await Task.Delay(500, ct);
                await ProcessAIMoveAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 新規ゲーム等でキャンセルされた後の例外は無視する（古いタスクが新ゲームを壊さないため）
            if (ct.IsCancellationRequested) return;
            StatusMessage = $"ターン処理エラー: {ex.Message}";
        }
    }

    private async Task ProcessAIMoveAsync(CancellationToken ct)
    {
        // スレッド安全性の前提: このメソッドの await 後は UI の SynchronizationContext に
        // 戻るため、_engine へのアクセスは実質 UI スレッドからのみとなる。
        // SynchronizationContext のないテスト環境や非 UI 利用では別途直列化が必要。
        //
        // エンジンが手番のないプレイヤーを CurrentPlayer に残さない（AdvanceTurn が自動スキップ）ため、
        // ここに来た時点で AI には必ず有効手がある。明示的なパス処理は不要。
        if (!_engine.GameState.IsGameInProgress())
            return;

        if (_ai == null)
        {
            StatusMessage = "AI が初期化されていません。新規ゲームを開始してください。";
            IsGameInProgress = false;
            return;
        }

        IsAIThinking = true;
        StatusMessage = $"AI（{AiColor.ToDisplayString()}）が考え中...";

        try
        {
            await Task.Delay(AiMoveDelayMs, ct);

            var aiColor = AiColor;
            var ai      = _ai;
            var bestMove = await Task.Run(() => ai.GetBestMove(_engine.CurrentBoard, aiColor), ct);

            ct.ThrowIfCancellationRequested();

            var moveResult = _engine.MakeMove(bestMove);
            _ = AnimateFlipsAsync(moveResult.FlippedPieces);
            NotifyIfPassed();
            OnPropertyChanged(nameof(IsSettingsEditable));
            RefreshBoardDisplay();
            UpdateScoreBoardState();

            if (_engine.GameState.IsGameInProgress())
            {
                if (_engine.CurrentPlayer == AiColor)
                {
                    await Task.Delay(AiTurnDelayMs, ct);
                    await ProcessAIMoveAsync(ct);
                }
                else if (_engine.LastPassedPlayer != HumanColor)
                {
                    // 人間がパスで飛ばされていない通常時のみ手番案内で上書きする
                    StatusMessage = $"あなたの番です（{HumanColor.ToDisplayString()}）";
                }
            }
            else
            {
                EndGame();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 新規ゲーム等でキャンセルされた後の例外は無視する（古いタスクが新ゲームを壊さないため）
            if (ct.IsCancellationRequested) return;
            StatusMessage = $"AI エラー: {ex.Message}";
            IsGameInProgress = false;
            (_ai as IDisposable)?.Dispose();
            _ai = null;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsAIThinking = false;
        }
    }

    /// <summary>
    /// 直前のターン遷移で強制パスが発生していたら、その旨をステータスに表示する。
    /// </summary>
    private void NotifyIfPassed()
    {
        if (_engine.LastPassedPlayer is not { } passed)
            return;

        string who = passed == HumanColor
            ? $"あなた（{passed.ToDisplayString()}）"
            : $"AI（{passed.ToDisplayString()}）";
        StatusMessage = $"{who} は打てる場所がないためパスしました";
    }

    private void OnUndo()
    {
        if (!_engine.Undo())
            return;

        if (_engine.GameState.IsGameInProgress() && _engine.CurrentPlayer == AiColor)
            _engine.Undo();

        OnPropertyChanged(nameof(IsSettingsEditable));
        RefreshBoardDisplay();
        UpdateScoreBoardState();
    }

    /// <summary>
    /// 反転する石の IsBeingFlipped を FlipAnimationDurationMs の間 true にして UI 層のアニメーションをトリガーする。
    /// fire-and-forget で呼び出す。
    /// </summary>
    private async Task AnimateFlipsAsync(IReadOnlyList<Position> flipped)
    {
        if (flipped.Count == 0) return;

        var targets = BoardSquares
            .Where(sq => flipped.Contains(sq.Position))
            .ToList();

        try
        {
            foreach (var sq in targets)
                sq.IsBeingFlipped = true;

            await Task.Delay(FlipAnimationDurationMs);
        }
        finally
        {
            foreach (var sq in targets)
                sq.IsBeingFlipped = false;
        }
    }

    private void RefreshBoardDisplay()
    {
        int index = 0;
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                BoardSquares[index].SetPiece(_engine.CurrentBoard.GetPiece(row, col));
                index++;
            }
        }

        var validSet = _engine.CurrentPlayer == HumanColor
            ? new HashSet<Position>(_engine.GetValidMoves(HumanColor))
            : new HashSet<Position>();

        foreach (var square in BoardSquares)
            square.IsValidMove = validSet.Contains(square.Position);

        UpdateCurrentPlayerDisplay();
    }

    private void UpdateScoreBoardState()
    {
        BlackScore = _engine.BlackScore;
        WhiteScore = _engine.WhiteScore;
        UpdateCurrentPlayerDisplay();
    }

    private void UpdateCurrentPlayerDisplay()
    {
        if (_engine.GameState.IsGameInProgress())
        {
            bool isHumanTurn = _engine.CurrentPlayer == HumanColor;
            string name = isHumanTurn
                ? $"あなた（{HumanColor.ToDisplayString()}）"
                : $"AI（{AiColor.ToDisplayString()}）";
            CurrentPlayerDisplay = $"{name} のターン";
        }
        else
        {
            var (winner, _, _) = _engine.GetResult();
            CurrentPlayerDisplay = winner == null
                ? "引き分け"
                : (winner == HumanColor ? "あなたの勝利!" : "AI の勝利!");
        }
    }

    private void EndGame()
    {
        IsGameInProgress = false;

        (_ai as IDisposable)?.Dispose();
        _ai = null;

        var (winner, blackCount, whiteCount) = _engine.GetResult();
        if (winner == null)
        {
            StatusMessage = $"引き分け (黒: {blackCount}, 白: {whiteCount})";
        }
        else
        {
            string winnerName = winner == HumanColor ? "あなた" : "AI";
            StatusMessage = $"{winnerName} の勝利 (黒: {blackCount}, 白: {whiteCount})";
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        (_ai as IDisposable)?.Dispose();
        _ai = null;
    }
}
