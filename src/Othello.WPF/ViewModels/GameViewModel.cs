using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.WPF.ViewModels;

/// <summary>
/// オセロゲーム全体を制御する主要 ViewModel。
/// GameEngine（ゲームロジック）と PythonSubprocessAI（AI）を保持し、
/// UI（MainWindow.xaml）へのデータバインディングを通じてゲームの状態を表示する。
///
/// ゲームの流れ:
///   StartNewGame → 人間のクリック（SquareClicked）→ CheckAndProcessNextTurn
///                → AI ターンなら ProcessAIMove → ループ
/// </summary>
public class GameViewModel : ViewModelBase, IDisposable
{
    /// <summary>ゲームロジックエンジン（盤面・ターン・Undo 管理）</summary>
    private readonly GameEngine _engine = new();

    /// <summary>
    /// Python AI プロセスのラッパー。
    /// StartNewGame で生成し、EndGame または StartNewGame（再呼び出し）時に Dispose する。
    /// null の場合はゲームが未開始またはすでに終了している。
    /// </summary>
    private PythonSubprocessAI? _pythonAI;

    /// <summary>
    /// 非同期処理のキャンセル制御。StartNewGame のたびに前のトークンをキャンセルして新しいものを生成する。
    /// ProcessAIMoveAsync / CheckAndProcessNextTurnAsync が旧ゲームの継続で状態を上書きしないよう制御する。
    /// </summary>
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

    /// <summary>8×8 = 64 個のマス ViewModel のコレクション（XAML UniformGrid にバインド）</summary>
    public ObservableCollection<BoardSquareViewModel> BoardSquares { get; } = new();

    /// <summary>黒の現在の石数（スコアパネルにバインド）</summary>
    public int BlackScore { get => _blackScore; set => SetProperty(ref _blackScore, value); }

    /// <summary>白の現在の石数（スコアパネルにバインド）</summary>
    public int WhiteScore { get => _whiteScore; set => SetProperty(ref _whiteScore, value); }

    /// <summary>現在のターン・勝敗結果を示す表示文字列（サイドパネルにバインド）</summary>
    public string CurrentPlayerDisplay { get => _currentPlayerDisplay; set => SetProperty(ref _currentPlayerDisplay, value); }

    /// <summary>ステータスバーに表示するメッセージ</summary>
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    /// <summary>
    /// 現在選択されている AI 難易度。
    /// 変更は次の StartNewGame 時に Python AI に反映される。
    /// </summary>
    public DifficultyLevel Difficulty { get => _difficulty; set => SetProperty(ref _difficulty, value); }

    /// <summary>ゲームが進行中かどうか（ボードのヒットテスト制御に使用）</summary>
    public bool IsGameInProgress
    {
        get => _isGameInProgress;
        set
        {
            if (SetProperty(ref _isGameInProgress, value))
                OnPropertyChanged(nameof(IsSettingsEditable)); // 連動して難易度・色の編集可否も更新
        }
    }

    /// <summary>難易度・担当色の設定変更が可能かどうか（ゲーム未開始 or 終了後のみ true）</summary>
    public bool IsSettingsEditable => !IsGameInProgress;

    /// <summary>AI が思考中かどうか（ボードの入力ロック・「考え中」表示に使用）</summary>
    public bool IsAIThinking { get => _isAIThinking; set => SetProperty(ref _isAIThinking, value); }

    /// <summary>
    /// 人間が担当するプレイヤーの色。
    /// 変更は次の StartNewGame 時に反映される。
    /// </summary>
    public PlayerColor HumanColor
    {
        get => _humanColor;
        set => SetProperty(ref _humanColor, value);
    }

    /// <summary>
    /// 難易度 ComboBox とのバインディング用 int インデックス。
    /// 0=イージー, 1=ノーマル, 2=ハード。
    /// DifficultyLevel の int 値（1〜3）と 1 ずれている点に注意。
    /// </summary>
    public int DifficultyIndex
    {
        get => (int)Difficulty - 1;
        set => Difficulty = (DifficultyLevel)(value + 1);
    }

    /// <summary>
    /// 人間の色 ComboBox とのバインディング用 int インデックス。
    /// 0=黒（先手）, 1=白（後手）。
    /// </summary>
    public int HumanColorIndex
    {
        get => HumanColor == PlayerColor.Black ? 0 : 1;
        set => HumanColor = value == 0 ? PlayerColor.Black : PlayerColor.White;
    }

    /// <summary>AI が担当する色（HumanColor の逆）</summary>
    private PlayerColor AiColor => HumanColor.Opponent();

    /// <summary>新規ゲームを開始するコマンド</summary>
    public ICommand NewGameCommand { get; }

    /// <summary>マスをクリックしたときのコマンド（CommandParameter に Position を渡す）</summary>
    public ICommand SquareClickedCommand { get; }

    /// <summary>パスするコマンド（ゲーム進行中かつ AI 思考中でない場合のみ実行可能）</summary>
    public ICommand PassCommand { get; }

    /// <summary>1 手戻すコマンド（ゲーム進行中かつ AI 思考中でない場合のみ実行可能）</summary>
    public ICommand UndoCommand { get; }

    /// <summary>
    /// GameViewModel を生成し、コマンドを初期化してゲームを開始する。
    /// </summary>
    public GameViewModel()
    {
        NewGameCommand       = new RelayCommand(StartNewGame);
        SquareClickedCommand = new RelayCommand<Position>(SquareClicked);
        // Pass は人間のターンかつ有効手がない場合のみ有効（有効手があるのにパスしようとすると例外になるため）
        PassCommand = new RelayCommand(OnPass, () =>
            _engine.GameState.IsGameInProgress() &&
            !IsAIThinking &&
            _engine.CurrentPlayer == HumanColor &&
            _engine.GetValidMoves(HumanColor).Count == 0);
        UndoCommand = new RelayCommand(OnUndo, () => _engine.GameState.IsGameInProgress() && !IsAIThinking);

        // 64 個のマス ViewModel を生成してバインディング用コレクションに登録する
        InitializeBoard();
        StartNewGame();
    }

    /// <summary>
    /// 8×8 = 64 個の BoardSquareViewModel を行優先で生成し、BoardSquares に追加する。
    /// ゲーム開始時に一度だけ呼ばれる（ゲームリセット時は再生成しない）。
    /// </summary>
    private void InitializeBoard()
    {
        BoardSquares.Clear();
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                BoardSquares.Add(new BoardSquareViewModel(new Position(row, col)));
    }

    /// <summary>
    /// 新しいゲームを開始する。
    /// 旧 AI プロセスを Dispose した後、新しい PythonSubprocessAI を生成して初期化する。
    /// AI が先手（黒）の場合は即座に AI ターンを処理する。
    /// </summary>
    public void StartNewGame()
    {
        // 進行中の非同期処理をキャンセルして新しいトークンを生成する
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsAIThinking = false;

        // 前のゲームの Python プロセスを終了する
        _pythonAI?.Dispose();
        _pythonAI = null;

        // Python AI プロセスの起動に失敗した場合はエラーを表示してゲームを開始しない
        try
        {
            // 出力ディレクトリの Othello.Python/ai.py を Python スクリプトとして指定する
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
        RefreshBoardDisplay();
        IsGameInProgress = true;

        string humanStr = HumanColor.ToDisplayString();
        string aiStr    = AiColor.ToDisplayString();
        StatusMessage = $"ゲーム開始 - あなた: {humanStr}, AI: {aiStr}";
        UpdateScoreBoardState();

        // AI が黒（先手）の場合、ゲーム開始直後に AI ターンを処理する
        if (AiColor == PlayerColor.Black)
            _ = ProcessAIMoveAsync(_cts.Token);
    }

    /// <summary>
    /// マスがクリックされたときの処理。
    /// AI 思考中・ゲーム未進行・AI のターンの場合はクリックを無視する。
    /// </summary>
    /// <param name="pos">クリックされたマスの座標</param>
    private void SquareClicked(Position pos)
    {
        // AI 思考中またはゲーム終了後はクリックを受け付けない
        if (!IsGameInProgress || IsAIThinking)
            return;

        // 人間のターンでなければ（AI のターン）クリックを無視する
        if (_engine.CurrentPlayer != HumanColor)
            return;

        HandlePlayerMove(pos);
    }

    /// <summary>
    /// 人間プレイヤーの着手を処理する。
    /// 無効な手の場合はステータスに理由を表示して返す。
    /// </summary>
    /// <param name="position">着手する座標</param>
    private void HandlePlayerMove(Position position)
    {
        var result = _engine.MakeMove(position);
        if (!result.IsSuccess)
        {
            // 無効な手の理由をステータスバーに表示する
            StatusMessage = result.Message;
            return;
        }

        RefreshBoardDisplay();
        _ = CheckAndProcessNextTurnAsync(_cts!.Token);
    }

    /// <summary>
    /// 着手後の次のターン処理を確認し、AI ターンであれば AI 移動を非同期で開始する。
    /// ゲームが終了していれば EndGame を呼び出す。
    /// </summary>
    private async Task CheckAndProcessNextTurnAsync(CancellationToken ct)
    {
        try
        {
            UpdateScoreBoardState();

            // ゲームが終了した場合は終了処理へ
            if (!_engine.GameState.IsGameInProgress())
            {
                EndGame();
                return;
            }

            // AI のターンの場合は少し待ってから AI 移動を処理する
            if (_engine.CurrentPlayer == AiColor)
            {
                await Task.Delay(500, ct); // UI の更新を視覚的に見やすくするための短い待機
                await ProcessAIMoveAsync(ct);
            }
            // 人間のターンの場合はクリック待ち状態になる（何もしない）
        }
        catch (OperationCanceledException)
        {
            // 新規ゲーム開始による正常なキャンセル
        }
        catch (Exception ex)
        {
            StatusMessage = $"ターン処理エラー: {ex.Message}";
        }
    }

    /// <summary>
    /// AI ターンの移動を非同期で処理する。
    /// AI の計算はスレッドプールで実行し、UI スレッドをブロックしない。
    /// 計算中は IsAIThinking = true に設定してボードをロックする。
    /// ct がキャンセルされた場合（新規ゲーム開始）は OperationCanceledException で正常終了する。
    /// </summary>
    private async Task ProcessAIMoveAsync(CancellationToken ct)
    {
        // ゲームが既に終了している場合は処理しない
        if (!_engine.GameState.IsGameInProgress())
            return;

        // AI の有効手を確認する（有効手なしの場合はパスを実行する）
        var validMoves = _engine.GetValidMoves(_engine.CurrentPlayer);
        if (validMoves.Count == 0)
        {
            OnPass();
            return;
        }

        // _pythonAI が null のときは起動失敗済み → エラーを表示して終了
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
            // 短い待機を挟むことで AI 思考中の表示を視覚的に確認できるようにする
            // ct がキャンセルされると OperationCanceledException がスローされる
            await Task.Delay(300, ct);

            // AI の計算はスレッドプールで実行（UI スレッドをブロックしない）
            var aiColor  = AiColor;
            var pythonAI = _pythonAI; // ローカルに保持してスレッド間の null 化を防ぐ
            var bestMove = await Task.Run(() => pythonAI.GetBestMove(_engine.CurrentBoard, aiColor), ct);

            // GetBestMove 実行中にキャンセルされていた場合はここで止まる
            ct.ThrowIfCancellationRequested();

            _engine.MakeMove(bestMove);
            RefreshBoardDisplay();
            UpdateScoreBoardState();

            if (_engine.GameState.IsGameInProgress())
            {
                if (_engine.CurrentPlayer == AiColor)
                {
                    // 相手（人間）がパスしたため AI が連続してターンを持つ場合
                    await Task.Delay(500, ct);
                    await ProcessAIMoveAsync(ct);
                }
                else
                {
                    // 人間のターン: 「考え中」メッセージをクリアして入力待ちを明示する
                    StatusMessage = $"あなたの番です（{HumanColor.ToDisplayString()}）";
                }
            }
            else
            {
                // AI の着手でゲームが終了した場合
                EndGame();
            }
        }
        catch (OperationCanceledException)
        {
            // 新規ゲーム開始による正常なキャンセル。状態は StartNewGame が管理するため何もしない。
        }
        catch (Exception ex)
        {
            // Python プロセス異常・JSON 解析失敗・その他の予期しないエラーを UI に表示する
            StatusMessage = $"AI エラー: {ex.Message}";
            IsGameInProgress = false;
            _pythonAI?.Dispose();
            _pythonAI = null;
        }
        finally
        {
            // キャンセルされていない場合のみ IsAIThinking をリセットする。
            // キャンセル時は StartNewGame が既に false を設定済みで新ゲームが使用中のため上書きしない。
            if (!ct.IsCancellationRequested)
                IsAIThinking = false;
        }
    }

    /// <summary>
    /// 現在のプレイヤーのパスを処理する。
    /// パス後、次が AI のターンであれば AI 移動を処理する。
    /// </summary>
    private void OnPass()
    {
        try
        {
            _engine.Pass();
        }
        catch (InvalidOperationException ex)
        {
            // CanExecute のガードをすり抜けた場合の安全策
            StatusMessage = ex.Message;
            return;
        }
        RefreshBoardDisplay();
        UpdateScoreBoardState();

        if (_engine.GameState.IsGameInProgress())
        {
            // パス後に AI のターンになった場合は AI 移動を処理する
            if (_engine.CurrentPlayer == AiColor)
                _ = ProcessAIMoveAsync(_cts!.Token);
        }
        else
        {
            // パスによりゲームが終了した場合
            EndGame();
        }
    }

    /// <summary>
    /// 1 手を取り消す（Undo）。
    /// Undo 後に AI のターンになった場合は、さらに 1 手戻して人間のターンにする。
    /// これにより、Undo は常に人間が次に着手できる状態に戻る。
    /// </summary>
    private void OnUndo()
    {
        if (!_engine.Undo())
            return; // 履歴がない場合は何もしない

        // Undo の結果が AI のターンになった場合は、さらに 1 手戻す
        // （人間 → AI → 人間 の 2 手を 1 回の Undo 操作でまとめて戻す）
        if (_engine.GameState.IsGameInProgress() && _engine.CurrentPlayer == AiColor)
            _engine.Undo();

        RefreshBoardDisplay();
        UpdateScoreBoardState();
    }

    /// <summary>
    /// 全 64 マスの表示を現在の盤面状態に合わせて更新し、
    /// 人間のターンのみ有効手を黄色でハイライトする。
    /// </summary>
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

        // 人間のターンのみ有効手をハイライトする（AI のターン中は表示しない）
        // HashSet で O(1) ルックアップにして 64 マス分の線形探索を回避する
        var validSet = _engine.CurrentPlayer == HumanColor
            ? new HashSet<Position>(_engine.GetValidMoves(HumanColor))
            : new HashSet<Position>();

        foreach (var square in BoardSquares)
            square.IsValidMove = validSet.Contains(square.Position);

        UpdateCurrentPlayerDisplay();
    }

    /// <summary>
    /// スコアと現在プレイヤー表示を盤面の最新状態に同期させる。
    /// </summary>
    private void UpdateScoreBoardState()
    {
        BlackScore = _engine.BlackScore;
        WhiteScore = _engine.WhiteScore;
        UpdateCurrentPlayerDisplay();
    }

    /// <summary>
    /// CurrentPlayerDisplay を現在のゲーム状態に合わせて更新する。
    /// 進行中はターン表示、終了後は勝敗結果を表示する。
    /// </summary>
    private void UpdateCurrentPlayerDisplay()
    {
        if (_engine.GameState.IsGameInProgress())
        {
            // ターン表示: 人間 or AI どちらのターンかを日本語で表示する
            bool isHumanTurn = _engine.CurrentPlayer == HumanColor;
            string name = isHumanTurn
                ? $"あなた（{HumanColor.ToDisplayString()}）"
                : $"AI（{AiColor.ToDisplayString()}）";
            CurrentPlayerDisplay = $"{name} のターン";
        }
        else
        {
            // ゲーム終了後: 勝敗結果を表示する
            var (winner, _, _) = _engine.GetResult();
            CurrentPlayerDisplay = winner == null
                ? "引き分け"                            // 同数の場合
                : (winner == HumanColor ? "あなたの勝利!" : "AI の勝利!");
        }
    }

    /// <summary>
    /// ゲーム終了処理を行う。
    /// Python AI プロセスを Dispose し、勝敗結果をステータスバーに表示する。
    /// </summary>
    private void EndGame()
    {
        IsGameInProgress = false;

        // ゲーム終了後は Python プロセスを解放する
        _pythonAI?.Dispose();
        _pythonAI = null;

        var (winner, blackCount, whiteCount) = _engine.GetResult();
        if (winner == null)
        {
            // 同数 → 引き分け
            StatusMessage = $"引き分け (黒: {blackCount}, 白: {whiteCount})";
        }
        else
        {
            // 勝者を「あなた」または「AI」と表示する
            string winnerName = winner == HumanColor ? "あなた" : "AI";
            StatusMessage = $"{winnerName} の勝利 (黒: {blackCount}, 白: {whiteCount})";
        }
    }

    /// <summary>
    /// 保持しているリソースを解放する。
    /// ウィンドウ閉鎖時に呼ばれ、進行中の AI 処理を止めて Python プロセスを確実に終了させる。
    /// </summary>
    public void Dispose()
    {
        // 進行中の非同期処理をキャンセルする
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Python プロセスを終了する（対局中にウィンドウを閉じてもリークしないように）
        _pythonAI?.Dispose();
        _pythonAI = null;
    }
}
