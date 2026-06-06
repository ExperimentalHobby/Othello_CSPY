using System.Collections.ObjectModel;
using System.IO;
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
    private readonly GameEngine _engine = new();
    private PythonSubprocessAI? _pythonAI;
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

    // Pass/Undo は RaiseCanExecuteChanged を呼べるよう RelayCommand 型で保持する
    private readonly RelayCommand _passCommand;
    private readonly RelayCommand _undoCommand;

    public ObservableCollection<BoardSquareViewModel> BoardSquares { get; } = new();

    public int BlackScore { get => _blackScore; set => SetProperty(ref _blackScore, value); }
    public int WhiteScore { get => _whiteScore; set => SetProperty(ref _whiteScore, value); }
    public string CurrentPlayerDisplay { get => _currentPlayerDisplay; set => SetProperty(ref _currentPlayerDisplay, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public DifficultyLevel Difficulty { get => _difficulty; set => SetProperty(ref _difficulty, value); }

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
        set => SetProperty(ref _humanColor, value);
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
    public ICommand PassCommand => _passCommand;
    public ICommand UndoCommand => _undoCommand;

    public GameViewModel()
    {
        NewGameCommand       = new RelayCommand(StartNewGame);
        SquareClickedCommand = new RelayCommand<Position>(SquareClicked);

        _passCommand = new RelayCommand(OnPass, () =>
            _engine.GameState.IsGameInProgress() &&
            !IsAIThinking &&
            _engine.CurrentPlayer == HumanColor &&
            _engine.GetValidMoves(HumanColor).Count == 0);

        _undoCommand = new RelayCommand(OnUndo, () =>
            _engine.GameState.IsGameInProgress() && !IsAIThinking);

        InitializeBoard();
        StartNewGame();
    }

    /// <summary>
    /// IsAIThinking / IsGameInProgress 変更時に Pass・Undo の CanExecute を再評価する。
    /// WPF では CommandManager が担うが WinUI3 では手動通知が必要。
    /// </summary>
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(IsAIThinking) or nameof(IsGameInProgress))
        {
            _passCommand?.RaiseCanExecuteChanged();
            _undoCommand?.RaiseCanExecuteChanged();
        }
    }

    private void InitializeBoard()
    {
        BoardSquares.Clear();
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                BoardSquares.Add(new BoardSquareViewModel(new Position(row, col)));
    }

    public void StartNewGame()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsAIThinking = false;

        _pythonAI?.Dispose();
        _pythonAI = null;

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Othello.Python", "ai.py");
            _pythonAI = new PythonSubprocessAI(Difficulty, scriptPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI の起動に失敗しました: {ex.Message}";
            IsGameInProgress = false;
            return;
        }

        _engine.Initialize();
        OnPropertyChanged(nameof(IsSettingsEditable));
        RefreshBoardDisplay();
        IsGameInProgress = true;

        string humanStr = HumanColor.ToDisplayString();
        string aiStr    = AiColor.ToDisplayString();
        StatusMessage = $"ゲーム開始 - あなた: {humanStr}, AI: {aiStr}";
        UpdateScoreBoardState();

        if (AiColor == PlayerColor.Black)
            _ = ProcessAIMoveAsync(_cts.Token);
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
            StatusMessage = $"ターン処理エラー: {ex.Message}";
        }
    }

    private async Task ProcessAIMoveAsync(CancellationToken ct)
    {
        if (!_engine.GameState.IsGameInProgress())
            return;

        var validMoves = _engine.GetValidMoves(_engine.CurrentPlayer);
        if (validMoves.Count == 0)
        {
            OnPass();
            return;
        }

        if (_pythonAI == null)
        {
            StatusMessage = "AI が初期化されていません。新規ゲームを開始してください。";
            IsGameInProgress = false;
            return;
        }

        IsAIThinking = true;
        StatusMessage = $"AI（{AiColor.ToDisplayString()}）が考え中...";

        try
        {
            await Task.Delay(300, ct);

            var aiColor  = AiColor;
            var pythonAI = _pythonAI;
            var bestMove = await Task.Run(() => pythonAI.GetBestMove(_engine.CurrentBoard, aiColor), ct);

            ct.ThrowIfCancellationRequested();

            _engine.MakeMove(bestMove);
            OnPropertyChanged(nameof(IsSettingsEditable));
            RefreshBoardDisplay();
            UpdateScoreBoardState();

            if (_engine.GameState.IsGameInProgress())
            {
                if (_engine.CurrentPlayer == AiColor)
                {
                    await Task.Delay(500, ct);
                    await ProcessAIMoveAsync(ct);
                }
                else
                {
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
            StatusMessage = $"AI エラー: {ex.Message}";
            IsGameInProgress = false;
            _pythonAI?.Dispose();
            _pythonAI = null;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsAIThinking = false;
        }
    }

    private void OnPass()
    {
        try
        {
            _engine.Pass();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return;
        }
        RefreshBoardDisplay();
        UpdateScoreBoardState();

        if (_engine.GameState.IsGameInProgress())
        {
            if (_engine.CurrentPlayer == AiColor)
                _ = ProcessAIMoveAsync(_cts!.Token);
        }
        else
        {
            EndGame();
        }
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

        _pythonAI?.Dispose();
        _pythonAI = null;

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

        _pythonAI?.Dispose();
        _pythonAI = null;
    }
}
