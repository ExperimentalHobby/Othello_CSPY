namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
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
		public string EngineName => "AI: Fake";

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
		public string EngineName => "AI: Fake";

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

		// Beginner=0, Easy=1, Normal=2, Hard=3, Expert=4
		vm.DifficultyIndex = 0;
		Assert.Equal(DifficultyLevel.Beginner, vm.Difficulty);
		vm.DifficultyIndex = 1;
		Assert.Equal(DifficultyLevel.Easy, vm.Difficulty);
		vm.DifficultyIndex = 3;
		Assert.Equal(DifficultyLevel.Hard, vm.Difficulty);
		Assert.Equal(3, vm.DifficultyIndex);
		vm.DifficultyIndex = 4;
		Assert.Equal(DifficultyLevel.Expert, vm.Difficulty);
		Assert.Equal(4, vm.DifficultyIndex);

		vm.HumanColorIndex = 0;
		Assert.Equal(PlayerColor.Black, vm.HumanColor);
		Assert.Equal(0, vm.HumanColorIndex);
	}

	/// <summary>
	/// 初手前に難易度を変更すると、新しい難易度で AI を作り直してゲームが再起動することを確認する（#3）。
	/// パス条件: 難易度変更後にファクトリが再度呼ばれ、生成された AI の Difficulty が新しい値であること。
	/// 注: RestartIfConfiguringBeforeFirstMove() は StartNewGameAsync()（非同期）を使うため、
	///     ファクトリ呼び出し完了まで待機してから検証する。
	/// </summary>
	[Fact]
	public void ChangingDifficulty_BeforeFirstMove_RestartsWithNewDifficulty()
	{
		int created = 0;
		FakeAI? last = null;
		using var vm = new GameViewModel(d => { created++; last = new FakeAI(d); return last; });

		Assert.Equal(1, created);

		vm.DifficultyIndex = 3; // Hard へ変更（index 3 = Hard）→ 初期状態なので非同期再起動

		// 非同期再起動の完了を待機（FakeAI の生成は瞬時なので短時間で確認できる）
		Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref created) == 2, Timeout));
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

	// ========== #3: Undo ==========

	/// <summary>
	/// 人間が 1 手打った後に Undo すると初期スコア（2/2）に戻ることを確認する。
	/// BlockingFakeAI で AI をブロックしておくことでエンジン状態への競合を防ぐ。
	/// パス条件: Undo 後に BlackScore=2、WhiteScore=2 になること。
	/// </summary>
	[Fact]
	public void Undo_AfterHumanMove_RestoresInitialScore()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		// 人間（黒）が着手 → AI（白）が GetBestMove に入ってブロックする
		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(entered.Wait(Timeout));

		// AI がブロック中に Undo を呼ぶ（RelayCommand.Execute は CanExecute を無視して直接 OnUndo を呼ぶ）
		vm.UndoCommand.Execute(null);

		Assert.Equal(2, vm.BlackScore);
		Assert.Equal(2, vm.WhiteScore);
	}

	/// <summary>
	/// AI が着手した後に Undo すると、AI と人間の 2 手分がまとめて取り消されて初期スコアに戻ることを確認する
	/// （OnUndo の「AI ターンに戻った場合はさらに 1 回 Undo」動作の検証）。
	/// パス条件: Undo 後に BlackScore=2、WhiteScore=2 になること。
	/// </summary>
	[Fact]
	public void Undo_AfterAiResponds_UndoesBothMovesAndRestoresInitialScore()
	{
		using var aiMoved = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new FakeAI(d, () => aiMoved.Set()));

		// 人間（黒）が着手 → AI（白）が応答
		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(aiMoved.Wait(Timeout));

		// AI の GetBestMove 後にエンジンへの MakeMove が非同期で完了するまで待機する
		// （ProcessAIMoveAsync 内で await Task.Run 完了 → _engine.MakeMove の順で実行される）
		Thread.Sleep(500);

		vm.UndoCommand.Execute(null);

		Assert.Equal(2, vm.BlackScore);
		Assert.Equal(2, vm.WhiteScore);
	}

	// ========== #55: Undo 時の棋譜(_kifuMoves)巻き戻し ==========

	/// <summary>
	/// 人間が 1 手打った後に Undo すると、棋譜収集用リストからもその手が取り除かれることを確認する。
	/// パス条件: Undo 後に KifuMovesForTest が空であること。
	/// </summary>
	[Fact]
	public void Undo_AfterHumanMove_RemovesKifuMoveEntry()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(entered.Wait(Timeout));

		vm.UndoCommand.Execute(null);

		Assert.Empty(vm.KifuMovesForTest);
	}

	/// <summary>
	/// AI が応答着手した後に Undo すると、人間・AI 両方の手が棋譜収集用リストから取り除かれることを確認する。
	/// パス条件: Undo（2 回分）後に KifuMovesForTest が空であること。
	/// </summary>
	[Fact]
	public void Undo_AfterAiResponds_RemovesBothKifuMoveEntries()
	{
		using var aiMoved = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new FakeAI(d, () => aiMoved.Set()));

		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(aiMoved.Wait(Timeout));

		Thread.Sleep(500);

		vm.UndoCommand.Execute(null);

		Assert.Empty(vm.KifuMovesForTest);
	}

	/// <summary>
	/// 直後に相手が強制パスする局面で人間が着手し Undo すると、
	/// 本手とそれに付随するパス記録がまとめて正しく巻き戻ることを確認する（境界値: undoCount=1 で 2 件巻き戻し）。
	/// パス条件: Undo 後に KifuMovesForTest が空であること。
	/// </summary>
	[Fact]
	public void Undo_AcrossForcedPass_RemovesMoveAndPassEntryTogether()
	{
		// 全マス黒で埋め、(0,0)/(7,7) を空き、(0,1)/(7,6) を白にする。
		// 白は挟める石がなく有効手なし。黒が (0,0) に着手すると白は強制パスし、黒の手番のまま残る。
		var board = new Board();
		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				board.SetPiece(r, c, PlayerColor.Black);
		board.SetPiece(0, 0, PlayerColor.Empty);
		board.SetPiece(0, 1, PlayerColor.White);
		board.SetPiece(7, 7, PlayerColor.Empty);
		board.SetPiece(7, 6, PlayerColor.White);

		using var vm = new GameViewModel(d => new FakeAI(d));
		vm.LoadStateForTest(board, PlayerColor.Black); // 人間=黒（既定）

		vm.SquareClickedCommand.Execute(new Position(0, 0)); // 白は強制パス、黒の手番のまま
		Assert.Equal(2, vm.KifuMovesForTest.Count); // 本手 + パス記録

		vm.UndoCommand.Execute(null);

		Assert.Empty(vm.KifuMovesForTest);
	}

	/// <summary>
	/// Undo を挟んだ複数手の対局後、KifuMovesForTest を KifuPlayer に渡して再生すると
	/// 実際の盤面（EngineCurrentBoard）と完全一致することを確認する（結合テスト）。
	/// パス条件: KifuPlayer.GoToEnd() が例外を投げず、全 64 マスが実際の盤面と一致すること。
	/// </summary>
	[Fact]
	public void KifuPlayer_ReplayAfterUndo_MatchesActualBoard()
	{
		using var aiMoved = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new FakeAI(d, () => aiMoved.Set()));

		void PlayHumanMoveAndWaitForAi()
		{
			aiMoved.Reset();
			var move = vm.BoardSquares.First(sq => sq.IsValidMove).Position;
			vm.SquareClickedCommand.Execute(move);
			Assert.True(aiMoved.Wait(Timeout));
			Thread.Sleep(300); // ProcessAIMoveAsync 内の MakeMove 完了を待つ
		}

		PlayHumanMoveAndWaitForAi(); // 1 手目（人間 + AI）
		PlayHumanMoveAndWaitForAi(); // 2 手目（人間 + AI）

		vm.UndoCommand.Execute(null); // 2 手目をまとめて取り消す

		PlayHumanMoveAndWaitForAi(); // 打ち直し（人間 + AI）

		var actualBoard = vm.EngineCurrentBoard;
		var record = new KifuRecord(1, DateTimeOffset.Now,
			PlayerColor.Black, DifficultyLevel.Easy,
			Result: null, vm.KifuMovesForTest.ToList(), new KifuFinalScore(0, 0));
		var player = new KifuPlayer(record);

		var ex = Record.Exception(() => player.GoToEnd());
		Assert.Null(ex);

		for (int r = 0; r < 8; r++)
			for (int c = 0; c < 8; c++)
				Assert.Equal(actualBoard.GetPiece(r, c), player.CurrentBoard.GetPiece(r, c));
	}

	// ========== #4: UndoCommand.CanExecute ==========

	/// <summary>
	/// ゲーム進行中かつ人間のターンでは UndoCommand.CanExecute が true を返すことを確認する。
	/// パス条件: 初期状態（黒が先手、AI 非思考中）で CanExecute が true であること。
	/// </summary>
	[Fact]
	public void UndoCommand_CanExecute_TrueWhenHumanTurn()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		// デフォルト: 人間=黒（先手）、ゲーム進行中、AI 非思考中
		Assert.True(vm.IsGameInProgress);
		Assert.False(vm.IsAIThinking);
		Assert.True(vm.UndoCommand.CanExecute(null));
	}

	/// <summary>
	/// AI が思考中は UndoCommand.CanExecute が false を返すことを確認する。
	/// パス条件: BlockingFakeAI でブロック中に CanExecute が false であること。
	/// </summary>
	[Fact]
	public void UndoCommand_CanExecute_FalseWhenAiThinking()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		// 人間=白に変更 → AI（黒）が先手で思考開始してブロック
		vm.HumanColorIndex = 1;
		Assert.True(entered.Wait(Timeout));

		Assert.True(vm.IsAIThinking);
		Assert.False(vm.UndoCommand.CanExecute(null));
	}

	/// <summary>
	/// ゲームが進行中でない（IsGameInProgress=false）ときは UndoCommand.CanExecute が false を返すことを確認する。
	/// パス条件: AI ファクトリが例外を投げて IsGameInProgress=false になった後、CanExecute が false であること。
	/// </summary>
	[Fact]
	public void UndoCommand_CanExecute_FalseWhenGameNotInProgress()
	{
		// AI ファクトリが例外を投げると StartNewGame が早期 return し IsGameInProgress=false になる
		using var vm = new GameViewModel(_ => throw new InvalidOperationException("AI unavailable"));
		Assert.False(vm.IsGameInProgress);
		Assert.False(vm.UndoCommand.CanExecute(null));
	}

	// ========== #11: AI エラー時のゲーム停止 ==========

	/// <summary>
	/// AI が GetBestMove で例外を投げると IsGameInProgress が false になり、
	/// StatusMessage に "AI エラー" が含まれることを確認する。
	/// パス条件: AI 例外後に IsGameInProgress=false かつ StatusMessage に "AI エラー" が含まれること。
	/// </summary>
	[Fact]
	public void AiError_SetsIsGameInProgressFalseAndUpdatesStatus()
	{
		using var aiErrored = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new ErrorFakeAI(d, aiErrored));

		// 人間（黒）が着手 → AI（白）の GetBestMove が例外を送出
		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(aiErrored.Wait(Timeout));

		// ProcessAIMoveAsync の catch ブロックが走る猶予を与える
		Thread.Sleep(200);

		Assert.False(vm.IsGameInProgress);
		Assert.Contains("AI エラー", vm.StatusMessage);
	}

	// ========== #12: 無効クリック ==========

	/// <summary>
	/// AI 思考中に人間がマスをクリックしても盤面スコアが変わらないことを確認する（クリックが無視される）。
	/// パス条件: IsAIThinking=true の間にクリックしてもスコアが変化しないこと。
	/// </summary>
	[Fact]
	public void SquareClicked_WhenAiThinking_IsIgnored()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		// 人間=白 → AI（黒）が先手で思考開始してブロック
		vm.HumanColorIndex = 1;
		Assert.True(entered.Wait(Timeout));
		Assert.True(vm.IsAIThinking);

		int totalBefore = vm.BlackScore + vm.WhiteScore;
		// 人間（白）が AI 思考中にクリックしても無視される（IsAIThinking ガード）
		vm.SquareClickedCommand.Execute(new Position(2, 3));

		Assert.Equal(totalBefore, vm.BlackScore + vm.WhiteScore);
		Assert.True(vm.IsAIThinking);
	}

	/// <summary>
	/// 有効でないマスへのクリックで MoveResult.Failure が StatusMessage に反映されることを確認する。
	/// パス条件: 無効手 (0,0) をクリック後に StatusMessage が "有効" を含む失敗メッセージになること。
	/// </summary>
	[Fact]
	public void SquareClicked_InvalidPosition_SetsStatusMessage()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));

		// 初期盤面で黒が (0,0) に打つのは無効手（挟める石がない）
		string statusBefore = vm.StatusMessage;
		vm.SquareClickedCommand.Execute(new Position(0, 0));

		// HandlePlayerMove が MoveResult.Failure を受け取り StatusMessage を更新する
		Assert.NotEqual(statusBefore, vm.StatusMessage);
		Assert.Contains("有効", vm.StatusMessage);
	}

	// ========== #Undo競合: CheckAndProcessNextTurnAsync 遅延中の Undo ==========

	/// <summary>
	/// CheckAndProcessNextTurnAsync の 500ms 遅延中に Undo が実行されても、
	/// 遅延明けの AI が「人間の手番のまま MakeMove」しないことを確認する（競合バグ回帰）。
	///
	/// バグ再現の仕掛け:
	///   FixedMoveFakeAI は色を問わず Position(2,3) を返す。
	///   (2,3) は初期盤面で黒（HumanColor）の有効手である。
	///   Undo 後に _currentPlayer=Black のまま ProcessAIMoveAsync が続行すると
	///   MakeMove((2,3)) が黒として成功し、スコアが 4/1 に変わる（バグ）。
	///   修正済みなら CurrentPlayer != AiColor ガードで弾かれ 2/2 のまま（グリーン）。
	///
	/// パス条件: Undo 後のスコアが初期値 BlackScore=2 / WhiteScore=2 のままであること。
	/// </summary>
	[Fact]
	public async Task UndoDuringAiDelay_DoesNotMakeMoveOnHumanTurn()
	{
		// (2,3) は初期盤面で黒の有効手 → Undo 後に黒手番のままこの手が通ると黒石が増える
		using var vm = new GameViewModel(d => new FixedMoveFakeAI(d, new Position(2, 3)));

		// 人間（黒）が (2,3) に着手 → CheckAndProcessNextTurnAsync が 500ms 遅延に入る
		vm.SquareClickedCommand.Execute(new Position(2, 3));

		// 遅延中（IsAIThinking=false）に Undo を実行する
		await Task.Delay(50);
		vm.UndoCommand.Execute(null);

		// 遅延完了 + ProcessAIMoveAsync の AI 待機（300ms）を過ぎるまで待機
		await Task.Delay(900);

		Assert.Equal(2, vm.BlackScore);
		Assert.Equal(2, vm.WhiteScore);
	}

	// ========== AiEngineLabel ==========

	/// <summary>
	/// 注入した AI の EngineName が AiEngineLabel に反映されることを確認する。
	/// パス条件: AiEngineLabel が FakeAI.EngineName（"AI: Fake"）と一致すること。
	/// </summary>
	[Fact]
	public void AiEngineLabel_ReflectsInjectedAiEngineName()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		Assert.Equal("AI: Fake", vm.AiEngineLabel);
	}

	// ========== IsSettingsEditable ==========

	/// <summary>
	/// 初手前（InitialState）はゲーム進行中でも IsSettingsEditable が true を返すことを確認する。
	/// パス条件: IsGameInProgress=true かつ IsSettingsEditable=true であること。
	/// </summary>
	[Fact]
	public void IsSettingsEditable_TrueAtGameStart_BeforeFirstMove()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		Assert.True(vm.IsGameInProgress);
		Assert.True(vm.IsSettingsEditable);
	}

	/// <summary>
	/// 人間が初手を打った後は IsSettingsEditable が false を返すことを確認する。
	/// BlockingFakeAI で AI をブロックし、着手後の状態を安定して検証する。
	/// パス条件: 人間着手後に IsGameInProgress=true かつ IsSettingsEditable=false であること。
	/// </summary>
	[Fact]
	public void IsSettingsEditable_FalseAfterFirstMove()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(entered.Wait(Timeout)); // AI がブロック中 = 着手済み

		Assert.True(vm.IsGameInProgress);
		Assert.False(vm.IsSettingsEditable);
	}

	// ========== BoardSquares 表示 ==========

	/// <summary>
	/// ゲーム開始直後、人間（黒）ターンでは初期有効手 4 マスのみ IsValidMove=true になることを確認する。
	/// 初期盤面で黒の有効手: (2,3), (3,2), (4,5), (5,4)。
	/// パス条件: IsValidMove=true のマスがこの 4 座標と完全一致すること。
	/// </summary>
	[Fact]
	public void BoardSquares_ValidMovesHighlighted_OnHumanTurn()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));

		var expected = new HashSet<Position>
		{
			new(2, 3), new(3, 2), new(4, 5), new(5, 4)
		};
		var actual = vm.BoardSquares
			.Where(sq => sq.IsValidMove)
			.Select(sq => sq.Position)
			.ToHashSet();

		Assert.Equal(expected, actual);
	}

	/// <summary>
	/// AI 思考中（IsAIThinking=true）は IsValidMove=true のマスが存在しないことを確認する。
	/// パス条件: BlockingFakeAI がブロック中に BoardSquares に IsValidMove=true のマスがないこと。
	/// </summary>
	[Fact]
	public void BoardSquares_NoValidMoveHighlighted_DuringAiThinking()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		vm.HumanColorIndex = 1; // 白に変更 → AI（黒）が先手
		Assert.True(entered.Wait(Timeout));
		Assert.True(vm.IsAIThinking);

		Assert.DoesNotContain(vm.BoardSquares, sq => sq.IsValidMove);
	}

	/// <summary>
	/// 人間が (2,3) に着手した後、そのマスの Piece が Black になることを確認する。
	/// パス条件: 着手後に BoardSquares[(2,3)].Piece が PlayerColor.Black であること。
	/// </summary>
	[Fact]
	public void BoardSquares_StoneAppears_AfterHumanMove()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		var target = vm.BoardSquares.First(sq => sq.Position == new Position(2, 3));
		Assert.Equal(PlayerColor.Empty, target.Piece); // 着手前は空

		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(entered.Wait(Timeout));

		Assert.Equal(PlayerColor.Black, target.Piece);
	}

	// ========== スコア更新 ==========

	/// <summary>
	/// 人間が有効手に着手するとスコア（BlackScore / WhiteScore）が変化することを確認する。
	/// 黒が (2,3) に置くと (3,3) の白を裏返す: 黒 2→4, 白 2→1。
	/// パス条件: BlackScore=4, WhiteScore=1 になること。
	/// </summary>
	[Fact]
	public void Score_IncreasesAfterHumanMove()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		Assert.Equal(2, vm.BlackScore);
		Assert.Equal(2, vm.WhiteScore);

		vm.SquareClickedCommand.Execute(new Position(2, 3));
		Assert.True(entered.Wait(Timeout));

		Assert.Equal(4, vm.BlackScore);
		Assert.Equal(1, vm.WhiteScore);
	}

	// ========== CurrentPlayerDisplay ==========

	/// <summary>
	/// ゲーム開始直後、人間（黒）ターンの CurrentPlayerDisplay が正しい文字列を返すことを確認する。
	/// パス条件: CurrentPlayerDisplay が "あなた（黒）のターン" であること。
	/// </summary>
	[Fact]
	public void CurrentPlayerDisplay_ShowsHumanTurnText_AtGameStart()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		Assert.Equal("あなた（黒） のターン", vm.CurrentPlayerDisplay);
	}

	/// <summary>
	/// AI（黒）が先手で思考中の CurrentPlayerDisplay が AI ターン文字列を返すことを確認する。
	/// パス条件: BlockingFakeAI がブロック中に CurrentPlayerDisplay が "AI（黒）のターン" であること。
	/// </summary>
	[Fact]
	public void CurrentPlayerDisplay_ShowsAiTurnText_WhenAiThinking()
	{
		using var entered = new ManualResetEventSlim(false);
		using var vm = new GameViewModel(d => new BlockingFakeAI(d, entered));

		vm.HumanColorIndex = 1; // 白に変更 → AI（黒）が先手
		Assert.True(entered.Wait(Timeout));

		Assert.Equal("AI（黒） のターン", vm.CurrentPlayerDisplay);
	}

	// ========== ゲーム外クリック ==========

	/// <summary>
	/// IsGameInProgress=false のとき SquareClicked しても StatusMessage が変わらないことを確認する。
	/// パス条件: AI 起動失敗後にクリックしても StatusMessage が変化しないこと。
	/// </summary>
	[Fact]
	public void SquareClicked_WhenGameNotInProgress_IsIgnored()
	{
		using var vm = new GameViewModel(_ => throw new InvalidOperationException("AI unavailable"));
		Assert.False(vm.IsGameInProgress);

		string statusBefore = vm.StatusMessage;
		vm.SquareClickedCommand.Execute(new Position(2, 3));

		Assert.Equal(statusBefore, vm.StatusMessage);
	}

	// ========== StatusMessage ==========

	/// <summary>
	/// ゲーム開始後の StatusMessage に「ゲーム開始」と人間・AI の色情報が含まれることを確認する。
	/// パス条件: StatusMessage に "ゲーム開始" が含まれること。
	/// </summary>
	[Fact]
	public void StatusMessage_ContainsGameStartInfo_AfterNewGame()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		Assert.Contains("ゲーム開始", vm.StatusMessage);
	}

	// ========== ヒント機能テスト ==========

	/// <summary>
	/// IsHintEnabled が false（デフォルト）の時、全マスの IsHint が false のこと。
	/// パス条件: ゲーム開始後に BoardSquares の全要素で IsHint = false であること。
	/// </summary>
	[Fact]
	public void HintDisabled_AllSquaresIsHintFalse()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));

		Assert.False(vm.IsHintEnabled);
		Assert.All(vm.BoardSquares, sq => Assert.False(sq.IsHint));
	}

	/// <summary>
	/// IsHintEnabled を true にした後、人間のターン（黒、初期盤面）で
	/// いずれか 1 マスの IsHint が true になること。
	/// パス条件: 非同期でヒント計算が完了後、IsHint=true のマスがちょうど 1 つあること。
	/// </summary>
	[Fact]
	public async Task HintEnabled_HumanTurn_ExactlyOneSquareIsHint()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		// デフォルトは人間=黒（先手）で人間ターンから開始

		vm.IsHintEnabled = true;
		await Task.Delay(500); // ヒント計算完了を待つ

		Assert.Single(vm.BoardSquares, sq => sq.IsHint);
	}

	/// <summary>
	/// ヒントが有効な状態で IsHintEnabled を false にすると、全マスの IsHint が false になること。
	/// パス条件: IsHintEnabled を false にした後、全 BoardSquares で IsHint = false であること。
	/// </summary>
	[Fact]
	public async Task HintEnabled_ThenDisabled_AllSquaresIsHintFalse()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));

		vm.IsHintEnabled = true;
		await Task.Delay(500);

		vm.IsHintEnabled = false;
		await Task.Delay(50); // クリア反映を待つ

		Assert.All(vm.BoardSquares, sq => Assert.False(sq.IsHint));
	}

	/// <summary>
	/// AI ターン終了後（人間が着手 → AI が応答 → 再び人間ターン）にヒントが更新されること。
	/// パス条件: AI 着手後に再び人間ターンになると IsHint=true のマスがちょうど 1 つあること。
	/// </summary>
	[Fact]
	public async Task HintEnabled_AfterAiMove_HintRefreshed()
	{
		using var vm = new GameViewModel(d => new FakeAI(d));
		vm.IsHintEnabled = true;
		await Task.Delay(300); // 初期ヒント計算待ち

		// 人間（黒）が着手
		var humanMove = vm.BoardSquares.First(sq => sq.IsValidMove).Position;
		vm.SquareClickedCommand.Execute(humanMove);

		// AI（白）が応答して再び人間ターンになるまで待つ
		await Task.Delay(1500);

		// 人間ターンに戻ったらヒントが更新されているはず
		Assert.Single(vm.BoardSquares, sq => sq.IsHint);
	}
}

// ========== テスト専用 AI モック ==========
// （ErrorFakeAI は GetBestMove を呼ぶと例外を送出し、aiErrored イベントを発火する）
file sealed class ErrorFakeAI : IAIStrategy
{
	private readonly ManualResetEventSlim _errored;
	public DifficultyLevel Difficulty { get; }
	public string EngineName => "AI: Error";

	public ErrorFakeAI(DifficultyLevel difficulty, ManualResetEventSlim errored)
	{
		Difficulty = difficulty;
		_errored = errored;
	}

	public Position GetBestMove(Board board, PlayerColor playerColor)
	{
		_errored.Set();
		throw new InvalidOperationException("AI エラーのシミュレーション");
	}
}

/// <summary>
/// 色を問わず常に指定した固定座標を返す AI モック。
/// 「白手番として呼ばれたのに黒の有効手座標を返す」状況を再現し、
/// Undo 競合バグ（CurrentPlayer ガードなし）のテストに使用する。
/// </summary>
file sealed class FixedMoveFakeAI(DifficultyLevel difficulty, Position fixedMove) : IAIStrategy
{
	public DifficultyLevel Difficulty { get; } = difficulty;
	public string EngineName => "AI: FixedMove";
	public Position GetBestMove(Board board, PlayerColor playerColor) => fixedMove;
}
