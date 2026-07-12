using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;
using Technopro.Othello.Core.Rules;
using Technopro.Othello.Core.Settings;
using Technopro.Othello.Core.Stats;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// オセロゲーム全体を制御する主要 ViewModel。
/// GameEngine（ゲームロジック）と PythonSubprocessAI（AI）を保持し、
/// UI へのデータバインディングを通じてゲームの状態を表示する。
/// WPF・WinUI3 両方から参照される共有 ViewModel。
/// </summary>
public partial class GameViewModel : ViewModelBase, IDisposable
{
	/// <summary>AI 着手前に挿入する演出用待機時間（ミリ秒）。</summary>
	private const int AiMoveDelayMs = 300;
	/// <summary>AI の連続手番（自分のパス後など）の前に挿入する演出用待機時間（ミリ秒）。</summary>
	private const int AiTurnDelayMs = 500;
	/// <summary>石の反転アニメーション全体の所要時間（ミリ秒）。IsBeingFlipped が True を維持する時間。</summary>
	private const int FlipAnimationDurationMs = 300;

	/// <summary>ヒント計算専用の C# AI（depth=2）。Python プロセス不要で即応答。</summary>
	private readonly AlphaBetaAI _hintAi = new(DifficultyLevel.Easy);

	private readonly GameEngine _engine = new();

	/// <summary>難易度から AI 実装を生成するファクトリ（テスト時はモックを注入できる）。</summary>
	private readonly Func<DifficultyLevel, IAIStrategy> _aiFactory;

	/// <summary>CPU vs CPU モード専用 AI ファクトリ（既定は AlphaBetaAI）。</summary>
	private readonly Func<DifficultyLevel, IAIStrategy> _cpuVsCpuAiFactory;

	/// <summary>現在のゲームで使用している AI（IDisposable なら破棄時に Dispose する）。</summary>
	private IAIStrategy? _ai;
	private CancellationTokenSource? _cts;

	// --- バッキングフィールド ---
	private int _blackScore;
	private int _whiteScore;
	private string _currentPlayerDisplay = "黒のターン";
	private string _statusMessage = "ゲーム開始";
	private DifficultyLevel _difficulty = DifficultyLevel.Medium;
	private bool _isGameInProgress;
	private bool _isAIThinking;
	private bool _isHintEnabled;
	private PlayerColor _humanColor = PlayerColor.Black;
	private GameMode _gameMode = GameMode.HumanVsCpu;
	private DifficultyLevel _blackDifficulty = DifficultyLevel.Medium;
	private DifficultyLevel _whiteDifficulty = DifficultyLevel.Medium;
	private bool _isPaused;
	private int _cpuVsCpuDelayIndex = 1; // 0=500ms, 1=1000ms, 2=2000ms, 3=3000ms
	private IAIStrategy? _blackCpuAi;
	private IAIStrategy? _whiteCpuAi;

	// Undo は RaiseCanExecuteChanged を呼べるよう RelayCommand 型で保持する
	private readonly RelayCommand _undoCommand;
	private readonly RelayCommand _pauseCommand;

	// --- 棋譜収集 ---
	private readonly List<KifuMove> _kifuMoves = new();
	// RecordMove() 呼び出しごとに _kifuMoves へ追加したエントリ数（1 or 2）。
	// _engine.Undo() 1 回につき末尾から 1 件 pop して同数だけ _kifuMoves を巻き戻す（OnUndo 参照）。
	private readonly List<int> _kifuMoveEntryCounts = new();
	private KifuRecord? _lastKifuRecord;

	// --- スコア推移 ---
	public ObservableCollection<ScorePoint> ScoreHistory { get; } = new();

	// --- 棋力統計 ---
	private readonly IStatsRepository _statsRepo;

	/// <summary>棋力評価・統計 ViewModel（右パネルにバインドする）。</summary>
	public StatsViewModel Stats { get; }

	// --- 制限時間 ---
	private bool _isTimeLimitEnabled;
	private int _timeLimitSeconds;
	private int _remainingSeconds;
	private CancellationTokenSource? _timerCts;
	private readonly string? _settingsFilePath;

	/// <summary>直近のゲームの棋譜。ゲーム終了後に設定され、新規ゲーム開始時に null にリセットされる。</summary>
	public KifuRecord? LastKifuRecord
	{
		get => _lastKifuRecord;
		private set => SetProperty(ref _lastKifuRecord, value);
	}

	/// <summary>制限時間モードが有効かどうか。ゲーム開始前のみ変更可能。</summary>
	public bool IsTimeLimitEnabled
	{
		get => _isTimeLimitEnabled;
		set => SetProperty(ref _isTimeLimitEnabled, value);
	}

	/// <summary>1 手あたりの制限時間（秒）。ゲーム開始前のみ変更可能。</summary>
	public int TimeLimitSeconds
	{
		get => _timeLimitSeconds;
		set
		{
			if (value < 1) value = 1;
			SetProperty(ref _timeLimitSeconds, value);
		}
	}

	/// <summary>現在の TimeLimitSeconds を設定ファイルに保存する。UI 層から TextBox 確定時に呼ぶ。</summary>
	public void SaveTimeLimitSettings()
		=> OthelloSettingsManager.Save(new OthelloSettings { TimeLimitSeconds = _timeLimitSeconds }, _settingsFilePath);

	/// <summary>現在の手番の残り秒数。制限時間 OFF または AI ターン中は 0。</summary>
	public int RemainingSeconds
	{
		get => _remainingSeconds;
		private set => SetProperty(ref _remainingSeconds, value);
	}

	/// <summary>初手前またはゲーム終了後のみ制限時間設定を変更できる。</summary>
	public bool IsTimeLimitEditable => !IsGameInProgress || _engine.IsInitialState;

	/// <summary>タイマーが動いているかどうか（制限時間表示の可視性に使う）。</summary>
	public bool IsTimerRunning => RemainingSeconds > 0;

	/// <summary>残り時間が 10 秒以下かどうか（UI の色変化トリガー）。</summary>
	public bool IsTimerWarning => RemainingSeconds > 0 && RemainingSeconds <= 10;

	/// <summary>残り時間テキスト（WinUI3 など MultiBinding が使えない環境向け）。</summary>
	public string RemainingSecondsText => $"{RemainingSeconds}秒";

	/// <summary>テスト用: 現在の盤面を返す（ViewModel 外から操作できるよう公開）。</summary>
	internal Board EngineCurrentBoard => _engine.CurrentBoard;

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
		set
		{
			if (!SetProperty(ref _difficulty, value)) return;
			// 難易度に応じて制限時間モードのデフォルト値を更新する
			IsTimeLimitEnabled = value == DifficultyLevel.Hard;
			RestartIfConfiguringBeforeFirstMove();
		}
	}

	public bool IsGameInProgress
	{
		get => _isGameInProgress;
		set
		{
			if (SetProperty(ref _isGameInProgress, value))
			{
				OnPropertyChanged(nameof(IsSettingsEditable));
				OnPropertyChanged(nameof(IsTimeLimitEditable));
			}
		}
	}

	// 未着手（初期盤面）の間は設定変更を許可する
	public bool IsSettingsEditable => !IsGameInProgress || _engine.IsInitialState;

	public bool IsAIThinking { get => _isAIThinking; set => SetProperty(ref _isAIThinking, value); }

	/// <summary>
	/// ヒント（AI 推奨手）表示のオン/オフ。
	/// オンにすると人間ターン時に非同期でヒントを計算してボードに反映する。
	/// </summary>
	public bool IsHintEnabled
	{
		get => _isHintEnabled;
		set
		{
			if (!SetProperty(ref _isHintEnabled, value)) return;
			if (!value)
			{
				ClearHint();
			}
			else if (IsGameInProgress && _engine.CurrentPlayer == HumanColor)
			{
				_ = RefreshHintAsync(_cts!.Token);
			}
		}
	}

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

	/// <summary>
	/// 難易度 ComboBox とバインドする 0 始まりインデックス（Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4）。
	/// <see cref="DifficultyLevel"/> の整数値と 1:1 対応。
	/// </summary>
	public int DifficultyIndex
	{
		get => (int)Difficulty;
		set => Difficulty = (DifficultyLevel)value;
	}

	/// <summary>
	/// 人間の色 ComboBox とバインドする 0 始まりインデックス（黒=0, 白=1）。
	/// </summary>
	public int HumanColorIndex
	{
		get => HumanColor == PlayerColor.Black ? 0 : 1;
		set => HumanColor = value == 0 ? PlayerColor.Black : PlayerColor.White;
	}

	private PlayerColor AiColor => HumanColor.Opponent();

	/// <summary>
	/// 現在の対戦モード。
	/// CpuVsCpu に切り替えると現在のゲームを中断して「新規ゲーム待ち」状態に移行する。
	/// HumanVsCpu に切り替えると即座に新規ゲームを開始する。
	/// </summary>
	public GameMode GameMode
	{
		get => _gameMode;
		set
		{
			if (!SetProperty(ref _gameMode, value)) return;
			OnPropertyChanged(nameof(GameModeIndex));
			OnPropertyChanged(nameof(IsCpuVsCpu));
			OnPropertyChanged(nameof(IsHumanVsCpu));

			if (IsCpuVsCpu)
				StopCurrentGameForModeChange();
			else
				_ = StartNewGameAsync();
		}
	}

	/// <summary>対戦モード ComboBox とバインドする 0 始まりインデックス（HumanVsCpu=0, CpuVsCpu=1）。</summary>
	public int GameModeIndex
	{
		get => (int)_gameMode;
		set => GameMode = (GameMode)value;
	}

	/// <summary>
	/// CPU vs CPU モードへの切替時に現在のゲームを中断し「新規ゲーム待ち」状態に移行する。
	/// ゲーム進行中でも呼び出し可能。
	/// </summary>
	private void StopCurrentGameForModeChange()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();
		IsAIThinking = false;
		IsPaused = false;

		(_ai as IDisposable)?.Dispose();
		_ai = null;
		_blackCpuAi = null;
		_whiteCpuAi = null;

		IsGameInProgress = false;
		_engine.Initialize();
		_kifuMoves.Clear();
		_kifuMoveEntryCounts.Clear();
		LastKifuRecord = null;
		ScoreHistory.Clear();
		RecordCurrentScore();
		OnPropertyChanged(nameof(IsSettingsEditable));
		OnPropertyChanged(nameof(IsTimeLimitEditable));
		RefreshBoardDisplay();
		StatusMessage = "新規ゲームボタンで CPU vs CPU 対戦を開始してください";
		UpdateScoreBoardState();
	}

	/// <summary>現在 CPU vs CPU モードかどうか。</summary>
	public bool IsCpuVsCpu => _gameMode == GameMode.CpuVsCpu;

	/// <summary>現在 人間 vs CPU モードかどうか。</summary>
	public bool IsHumanVsCpu => _gameMode == GameMode.HumanVsCpu;

	/// <summary>CPU vs CPU 時の黒AI難易度。初手前であれば変更と同時にゲームを再起動する。</summary>
	public DifficultyLevel BlackDifficulty
	{
		get => _blackDifficulty;
		set
		{
			if (!SetProperty(ref _blackDifficulty, value)) return;
			OnPropertyChanged(nameof(BlackDifficultyIndex));
			if (IsCpuVsCpu) RestartIfConfiguringBeforeFirstMove();
		}
	}

	/// <summary>黒の難易度 ComboBox とバインドする 0 始まりインデックス（Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4）。</summary>
	public int BlackDifficultyIndex
	{
		get => (int)_blackDifficulty;
		set => BlackDifficulty = (DifficultyLevel)value;
	}

	/// <summary>CPU vs CPU 時の白AI難易度。初手前であれば変更と同時にゲームを再起動する。</summary>
	public DifficultyLevel WhiteDifficulty
	{
		get => _whiteDifficulty;
		set
		{
			if (!SetProperty(ref _whiteDifficulty, value)) return;
			OnPropertyChanged(nameof(WhiteDifficultyIndex));
			if (IsCpuVsCpu) RestartIfConfiguringBeforeFirstMove();
		}
	}

	/// <summary>白の難易度 ComboBox とバインドする 0 始まりインデックス（Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4）。</summary>
	public int WhiteDifficultyIndex
	{
		get => (int)_whiteDifficulty;
		set => WhiteDifficulty = (DifficultyLevel)value;
	}

	private static readonly int[] CpuVsCpuDelayOptions = { 500, 1000, 2000, 3000 };

	/// <summary>自動再生速度 ComboBox とバインドする 0 始まりインデックス（0=500ms, 1=1000ms, 2=2000ms, 3=3000ms）。</summary>
	public int CpuVsCpuDelayIndex
	{
		get => _cpuVsCpuDelayIndex;
		set
		{
			if (!SetProperty(ref _cpuVsCpuDelayIndex, Math.Clamp(value, 0, 3))) return;
			_cpuVsCpuDelayMs = CpuVsCpuDelayOptions[_cpuVsCpuDelayIndex];
			OnPropertyChanged(nameof(CpuVsCpuDelayMs));
		}
	}

	/// <summary>
	/// 現在の自動再生待機時間（ms）。インデックスから計算する。
	/// internal set: テスト時に 0 を設定して遅延をスキップできる。
	/// </summary>
	public int CpuVsCpuDelayMs
	{
		get => _cpuVsCpuDelayMs;
		internal set => SetProperty(ref _cpuVsCpuDelayMs, value);
	}

	private int _cpuVsCpuDelayMs = 1000;

	/// <summary>CPU vs CPU が一時停止中かどうか。</summary>
	public bool IsPaused
	{
		get => _isPaused;
		private set
		{
			if (!SetProperty(ref _isPaused, value)) return;
			OnPropertyChanged(nameof(PauseButtonContent));
		}
	}

	/// <summary>
	/// 一時停止 / 再開ボタンのラベル。
	/// 初手前の一時停止状態（新規ゲーム直後）は「開始」、途中一時停止は「再開」と表示する。
	/// </summary>
	public string PauseButtonContent => _isPaused
		? (_engine.IsInitialState ? "開始" : "再開")
		: "一時停止";

	public ICommand NewGameCommand { get; }
	public ICommand SquareClickedCommand { get; }
	public ICommand UndoCommand => _undoCommand;

	/// <summary>CPU vs CPU モードの一時停止 / 再開コマンド。</summary>
	public ICommand PauseCommand => _pauseCommand;

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
	/// <param name="settings">設定ファイル（null の場合はファイルから読み込む）。</param>
	/// <param name="cpuVsCpuAiFactory">CPU vs CPU モード専用 AI ファクトリ。null の場合は AlphaBetaAI を使う。テストで FakeAI を差し込める。</param>
	/// <param name="statsRepository">棋力統計リポジトリ。null の場合はファイル永続化実装を使う。</param>
	/// <param name="settingsFilePath">設定ファイルの保存先パス。null の場合は既定パス（%LOCALAPPDATA%）を使う。テストで一時ファイルを差し込める。</param>
	public GameViewModel(Func<DifficultyLevel, IAIStrategy>? aiFactory, bool startDeferred = false, OthelloSettings? settings = null, Func<DifficultyLevel, IAIStrategy>? cpuVsCpuAiFactory = null, IStatsRepository? statsRepository = null, string? settingsFilePath = null)
	{
		_aiFactory = aiFactory ?? CreateDefaultAI;
		_cpuVsCpuAiFactory = cpuVsCpuAiFactory ?? (d => new AlphaBetaAI(d));
		_statsRepo = statsRepository ?? new StatsRepository();
		Stats = new StatsViewModel(_statsRepo);
		_settingsFilePath = settingsFilePath;

		// 設定ファイルから制限時間を読み込む（注入されなければファイルまたはデフォルト 30 秒）
		var loadedSettings = settings ?? OthelloSettingsManager.Load(_settingsFilePath);
		_timeLimitSeconds = loadedSettings.TimeLimitSeconds;
		// 初期難易度（Medium）に従い制限時間モードは OFF
		_isTimeLimitEnabled = _difficulty == DifficultyLevel.Hard;

		NewGameCommand = new RelayCommand(() => _ = StartNewGameAsync());
		SquareClickedCommand = new RelayCommand<Position>(SquareClicked);

		_undoCommand = new RelayCommand(OnUndo, () =>
			!IsCpuVsCpu && _engine.GameState.IsGameInProgress() && !IsAIThinking);
		_pauseCommand = new RelayCommand(() => IsPaused = !_isPaused);

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
		if (propertyName is nameof(IsAIThinking) or nameof(IsGameInProgress) or nameof(IsCpuVsCpu))
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
		LastKifuRecord = null;

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
		// ローカルに保持して「自分のCTS」として後で再入チェックに使う（F3）
		var cts = new CancellationTokenSource();
		_cts = cts;
		IsAIThinking = false;
		IsPaused = false;
		LastKifuRecord = null;

		(_ai as IDisposable)?.Dispose();
		_ai = null;
		_blackCpuAi = null;
		_whiteCpuAi = null;

		// CPU vs CPU モード: AI を同期生成（AlphaBetaAI は I/O なし・再入チェック不要）
		if (IsCpuVsCpu)
		{
			_blackCpuAi = _cpuVsCpuAiFactory(BlackDifficulty);
			_whiteCpuAi = _cpuVsCpuAiFactory(WhiteDifficulty);
			ApplyNewGameState();
			return;
		}

		IAIStrategy? newAi = null;
		try
		{
			newAi = await Task.Run(() => _aiFactory(Difficulty));
		}
		catch (Exception ex)
		{
			StatusMessage = $"AI の起動に失敗しました: {ex.Message}";
			IsGameInProgress = false;
			return;
		}

		// await 完了後に別の StartNewGameAsync が既に始動していたら、
		// 新しく作った AI を破棄して早期 return する（二重初期化・プロセスリーク防止）（F3）
		if (cts.IsCancellationRequested)
		{
			(newAi as IDisposable)?.Dispose();
			return;
		}

		_ai = newAi;
		ApplyNewGameState();
	}

	/// <summary>
	/// AI 生成後の共通初期化処理（StartNewGame / StartNewGameAsync 両方から呼ばれる）。
	/// 常に UI スレッドから呼ばれる前提。AiEngineLabel の更新もここで行う。
	/// </summary>
	private void ApplyNewGameState()
	{
		AiEngineLabel = IsCpuVsCpu ? "AI vs AI" : _ai!.EngineName;

		_engine.Initialize();
		_kifuMoves.Clear();
		_kifuMoveEntryCounts.Clear();
		LastKifuRecord = null;
		ScoreHistory.Clear();
		RecordCurrentScore();
		OnPropertyChanged(nameof(IsSettingsEditable));
		OnPropertyChanged(nameof(IsTimeLimitEditable));
		RefreshBoardDisplay();
		IsGameInProgress = true;

		if (IsCpuVsCpu)
		{
			// 新規ゲーム直後は「開始」ボタン待ちの一時停止状態にする。
			// これにより初手が打たれる前に対戦モードを変更できる余地を確保する。
			IsPaused = true;
			StatusMessage = $"黒: {BlackDifficulty.ToDisplayString()}, 白: {WhiteDifficulty.ToDisplayString()} — 開始ボタンで対戦を始めてください";
			UpdateScoreBoardState();
			_ = ProcessCpuVsCpuTurnAsync(_cts!.Token);
		}
		else
		{
			string humanStr = HumanColor.ToDisplayString();
			string aiStr = AiColor.ToDisplayString();
			StatusMessage = $"ゲーム開始 - あなた: {humanStr}, AI: {aiStr}";
			UpdateScoreBoardState();

			if (AiColor == PlayerColor.Black)
				_ = ProcessAIMoveAsync(_cts!.Token);
		}
	}

	private void SquareClicked(Position pos)
	{
		if (IsCpuVsCpu) return;
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

		RecordMove(HumanColor, position);
		RecordCurrentScore();

		_ = AnimateFlipsAsync(result.FlippedPieces, _cts!.Token);
		NotifyIfPassed();
		OnPropertyChanged(nameof(IsSettingsEditable));
		OnPropertyChanged(nameof(IsTimeLimitEditable));
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
				await Task.Delay(AiTurnDelayMs, ct);
				// 遅延中に Undo 等で手番が変わっていた場合は AI 処理をスキップする
				if (_engine.CurrentPlayer != AiColor) return;
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
			var ai = _ai;
			var bestMove = await Task.Run(() => ai.GetBestMove(_engine.CurrentBoard, aiColor), ct);

			ct.ThrowIfCancellationRequested();

			// Undo が遅延中に実行され手番が人間に戻っている場合は着手しない
			if (_engine.CurrentPlayer != AiColor) return;

			var moveResult = _engine.MakeMove(bestMove);
			if (!moveResult.IsSuccess)
			{
				StatusMessage = $"AI の手が無効でした: {moveResult.Message}";
				IsGameInProgress = false;
				(_ai as IDisposable)?.Dispose();
				_ai = null;
				return;
			}

			RecordMove(aiColor, bestMove);
			RecordCurrentScore();
			_ = AnimateFlipsAsync(moveResult.FlippedPieces, ct);
			NotifyIfPassed();
			OnPropertyChanged(nameof(IsSettingsEditable));
			OnPropertyChanged(nameof(IsTimeLimitEditable));
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
	/// CPU vs CPU モードの自動対戦ループ。ゲーム終了まで黒・白交互に AI 着手を繰り返す。
	/// <see cref="IsPaused"/> が true の間は 100ms ごとにポーリングして待機する。
	/// </summary>
	private async Task ProcessCpuVsCpuTurnAsync(CancellationToken ct)
	{
		try
		{
			while (_engine.GameState.IsGameInProgress())
			{
				while (_isPaused)
					await Task.Delay(100, ct);

				ct.ThrowIfCancellationRequested();
				if (!_engine.GameState.IsGameInProgress()) break;

				var currentColor = _engine.CurrentPlayer;
				var ai = currentColor == PlayerColor.Black ? _blackCpuAi : _whiteCpuAi;
				if (ai == null) break;

				IsAIThinking = true;
				StatusMessage = $"AI（{currentColor.ToDisplayString()}）が考え中...";

				await Task.Delay(CpuVsCpuDelayMs, ct);

				var aiColor = currentColor;
				var aiRef = ai;
				var bestMove = await Task.Run(() => aiRef.GetBestMove(_engine.CurrentBoard, aiColor), ct);

				ct.ThrowIfCancellationRequested();
				if (_engine.CurrentPlayer != currentColor) break;

				var moveResult = _engine.MakeMove(bestMove);
				if (!moveResult.IsSuccess)
				{
					StatusMessage = $"AI の手が無効でした: {moveResult.Message}";
					IsGameInProgress = false;
					return;
				}

				RecordMove(currentColor, bestMove);
				RecordCurrentScore();
				_ = AnimateFlipsAsync(moveResult.FlippedPieces, ct);
				NotifyIfPassed();
				OnPropertyChanged(nameof(IsSettingsEditable));
				OnPropertyChanged(nameof(IsTimeLimitEditable));
				RefreshBoardDisplay();
				UpdateScoreBoardState();

				IsAIThinking = false;
			}

			if (!ct.IsCancellationRequested)
				EndGame();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			if (ct.IsCancellationRequested) return;
			StatusMessage = $"CPU vs CPU エラー: {ex.Message}";
			IsGameInProgress = false;
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

		string who = !IsCpuVsCpu && passed == HumanColor
			? $"あなた（{passed.ToDisplayString()}）"
			: $"AI（{passed.ToDisplayString()}）";
		StatusMessage = $"{who} は打てる場所がないためパスしました";
	}

	private void OnUndo()
	{
		if (!_engine.Undo())
			return;

		var undoCount = 1;
		if (_engine.GameState.IsGameInProgress() && _engine.CurrentPlayer == AiColor)
		{
			_engine.Undo();
			undoCount = 2;
		}

		// 初期エントリ (index 0) を残したまま末尾から削除する
		for (int i = 0; i < undoCount && ScoreHistory.Count > 1; i++)
			ScoreHistory.RemoveAt(ScoreHistory.Count - 1);

		// _engine.Undo() 1 回 = RecordMove() 1 回分なので、同じ回数だけ _kifuMoves も巻き戻す
		for (int i = 0; i < undoCount && _kifuMoveEntryCounts.Count > 0; i++)
		{
			int removeCount = _kifuMoveEntryCounts[^1];
			_kifuMoveEntryCounts.RemoveAt(_kifuMoveEntryCounts.Count - 1);
			_kifuMoves.RemoveRange(_kifuMoves.Count - removeCount, removeCount);
		}

		OnPropertyChanged(nameof(IsSettingsEditable));
		OnPropertyChanged(nameof(IsTimeLimitEditable));
		RefreshBoardDisplay();
		UpdateScoreBoardState();
	}

	/// <summary>
	/// 反転する石の IsBeingFlipped を FlipAnimationDurationMs の間 true にして UI 層のアニメーションをトリガーする。
	/// fire-and-forget で呼び出す。
	/// </summary>
	private async Task AnimateFlipsAsync(IReadOnlyList<Position> flipped, CancellationToken ct = default)
	{
		if (flipped.Count == 0) return;

		var targets = BoardSquares
			.Where(sq => flipped.Contains(sq.Position))
			.ToList();

		try
		{
			foreach (var sq in targets)
				sq.IsBeingFlipped = true;

			// キャンセル時は OperationCanceledException が発生するが finally で IsBeingFlipped を戻す
			await Task.Delay(FlipAnimationDurationMs, ct);
		}
		catch (OperationCanceledException) { }
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

		var validSet = !IsCpuVsCpu && _engine.CurrentPlayer == HumanColor
			? new HashSet<Position>(_engine.GetValidMoves(HumanColor))
			: new HashSet<Position>();

		foreach (var square in BoardSquares)
			square.IsValidMove = validSet.Contains(square.Position);

		ClearHint();
		if (IsHintEnabled && !IsCpuVsCpu && IsGameInProgress && _engine.CurrentPlayer == HumanColor)
			_ = RefreshHintAsync(_cts!.Token);

		UpdateCurrentPlayerDisplay();

		// 人間ターン開始時にタイマーを（再）起動する
		RestartTurnTimer();
	}

	// ===== 制限時間タイマー =====

	/// <summary>
	/// 人間ターン開始時にカウントダウンタイマーを起動する。
	/// 制限時間 OFF または AI ターンの場合は起動しない。
	/// </summary>
	private void RestartTurnTimer()
	{
		_timerCts?.Cancel();
		_timerCts?.Dispose();
		_timerCts = null;
		RemainingSeconds = 0;
		OnPropertyChanged(nameof(IsTimerRunning));
		OnPropertyChanged(nameof(IsTimerWarning));
		OnPropertyChanged(nameof(RemainingSecondsText));

		if (!IsTimeLimitEnabled || !IsGameInProgress || _engine.CurrentPlayer != HumanColor || IsCpuVsCpu)
			return;

		var cts = new CancellationTokenSource();
		_timerCts = cts;
		_ = RunTurnTimerAsync(cts.Token);
	}

	/// <summary>タイマーを停止して残り時間を 0 にリセットする。</summary>
	private void StopTurnTimer()
	{
		_timerCts?.Cancel();
		_timerCts?.Dispose();
		_timerCts = null;
		RemainingSeconds = 0;
		OnPropertyChanged(nameof(IsTimerRunning));
		OnPropertyChanged(nameof(IsTimerWarning));
		OnPropertyChanged(nameof(RemainingSecondsText));
	}

	/// <summary>
	/// 1 秒ごとにカウントダウンし、0 になったら有効手[0]を強制着手する。
	/// UI スレッドの SynchronizationContext で再開するため Dispatcher.Invoke 不要。
	/// </summary>
	private async Task RunTurnTimerAsync(CancellationToken ct)
	{
		RemainingSeconds = TimeLimitSeconds;
		OnPropertyChanged(nameof(IsTimerRunning));
		OnPropertyChanged(nameof(IsTimerWarning));

		try
		{
			while (RemainingSeconds > 0)
			{
				await Task.Delay(1000, ct);
				RemainingSeconds--;
				OnPropertyChanged(nameof(IsTimerRunning));
				OnPropertyChanged(nameof(IsTimerWarning));
				OnPropertyChanged(nameof(RemainingSecondsText));
			}
		}
		catch (OperationCanceledException) { return; }

		// 時間切れ
		if (IsGameInProgress && _engine.CurrentPlayer == HumanColor)
		{
			StatusMessage = "時間切れ！自動着手します";
			await ForcePlayForTestAsync();
		}
	}

	/// <summary>
	/// 有効手[0]を強制着手する。時間切れ処理とテストから呼ぶ。
	/// </summary>
	public async Task ForcePlayForTestAsync()
	{
		var validMoves = OthelloRules.GetValidMoves(_engine.CurrentBoard, HumanColor);
		if (validMoves.Count == 0) return;
		HandlePlayerMove(validMoves[0]);
		// AI 応答を少し待つ（テストで AI が動く時間を確保）
		await Task.Delay(10);
	}

	/// <summary>
	/// C# AI（depth=2）で推奨手を非同期計算し、該当マスの IsHint を true にする。
	/// 人間ターン時のみ呼ばれ、IsHintEnabled が false になった時点でキャンセルされる。
	/// </summary>
	private async Task RefreshHintAsync(CancellationToken ct)
	{
		var board = _engine.CurrentBoard.Clone();
		var player = HumanColor;
		Position hint;
		try
		{
			hint = await Task.Run(() => _hintAi.GetBestMove(board, player), ct);
		}
		catch (OperationCanceledException) { return; }
		catch { return; }

		if (ct.IsCancellationRequested || !IsHintEnabled) return;

		foreach (var sq in BoardSquares)
			sq.IsHint = sq.Position == hint;
	}

	private void ClearHint()
	{
		foreach (var sq in BoardSquares)
			sq.IsHint = false;
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
			if (IsCpuVsCpu)
			{
				CurrentPlayerDisplay = $"AI（{_engine.CurrentPlayer.ToDisplayString()}）のターン";
			}
			else
			{
				bool isHumanTurn = _engine.CurrentPlayer == HumanColor;
				string name = isHumanTurn
					? $"あなた（{HumanColor.ToDisplayString()}）"
					: $"AI（{AiColor.ToDisplayString()}）";
				CurrentPlayerDisplay = $"{name} のターン";
			}
		}
		else
		{
			var (winner, _, _) = _engine.GetResult();
			if (IsCpuVsCpu)
			{
				CurrentPlayerDisplay = winner == null
					? "引き分け"
					: $"AI（{winner.Value.ToDisplayString()}）の勝利!";
			}
			else
			{
				CurrentPlayerDisplay = winner == null
					? "引き分け"
					: (winner == HumanColor ? "あなたの勝利!" : "AI の勝利!");
			}
		}
	}

	private void EndGame()
	{
		StopTurnTimer();
		IsGameInProgress = false;

		(_ai as IDisposable)?.Dispose();
		_ai = null;
		_blackCpuAi = null;
		_whiteCpuAi = null;

		var (winner, blackCount, whiteCount) = _engine.GetResult();
		if (IsCpuVsCpu)
		{
			StatusMessage = winner == null
				? $"引き分け (黒: {blackCount}, 白: {whiteCount})"
				: $"AI（{winner.Value.ToDisplayString()}）の勝利 (黒: {blackCount}, 白: {whiteCount})";
		}
		else
		{
			StatusMessage = winner == null
				? $"引き分け (黒: {blackCount}, 白: {whiteCount})"
				: $"{(winner == HumanColor ? "あなた" : "AI")} の勝利 (黒: {blackCount}, 白: {whiteCount})";
		}

		LastKifuRecord = new KifuRecord(
			Version: 1,
			PlayedAt: DateTimeOffset.Now,
			HumanColor: IsCpuVsCpu ? PlayerColor.Black : HumanColor,
			Difficulty: IsCpuVsCpu ? BlackDifficulty : Difficulty,
			Result: winner,
			Moves: _kifuMoves.AsReadOnly(),
			FinalScore: new KifuFinalScore(blackCount, whiteCount));

		RecordStatsIfHumanGame(winner, blackCount, whiteCount);
	}

	private void RecordStatsIfHumanGame(PlayerColor? winner, int blackCount, int whiteCount)
	{
		if (IsCpuVsCpu) return;

		var moveCount = _kifuMoves.Count(m => !m.IsPass);
		var winMargin = winner == HumanColor
			? Math.Abs(blackCount - whiteCount)
			: 0;

		var stats = _statsRepo.Load();
		stats.RecordResult(winner, HumanColor, Difficulty, moveCount, winMargin);
		_statsRepo.Save(stats);
		Stats.Refresh();
	}

	/// <summary>
	/// 着手を棋譜に追加する。MakeMove 後に呼ぶことで LastPassedPlayer のパス情報も記録する。
	/// </summary>
	private void RecordMove(PlayerColor player, Position position)
	{
		int before = _kifuMoves.Count;
		_kifuMoves.Add(new KifuMove(player, position.Row, position.Column));
		if (_engine.LastPassedPlayer is { } passed)
			_kifuMoves.Add(new KifuMove(passed, IsPass: true));
		_kifuMoveEntryCounts.Add(_kifuMoves.Count - before);
	}

	private void RecordCurrentScore()
	{
		int black = 0, white = 0;
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
			{
				var piece = _engine.CurrentBoard.GetPiece(r, c);
				if (piece == PlayerColor.Black) black++;
				else if (piece == PlayerColor.White) white++;
			}
		ScoreHistory.Add(new ScorePoint(ScoreHistory.Count, black, white));
	}

	public void Dispose()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;

		_timerCts?.Cancel();
		_timerCts?.Dispose();
		_timerCts = null;

		(_ai as IDisposable)?.Dispose();
		_ai = null;
		_blackCpuAi = null;
		_whiteCpuAi = null;
	}
}
