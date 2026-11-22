namespace Technopro.Othello.Tests.E2E.WPF;

using FlaUI.Core.AutomationElements;
using Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>着手・Undo・新規ゲームなど、ユーザー操作に対する GUI テスト（XAML バインディングと実 AI の連携を検証）。</summary>
public class OperationTests
{
	/// <summary>難易度 ComboBox の「ビギナー」のインデックス。思考待ちを短くするため全テストで選ぶ。</summary>
	private const int BeginnerIndex = 0;

	/// <summary>人間（黒）の手番を示す手番表示。</summary>
	private const string HumanTurnText = "あなた（黒）";

	/// <summary>ゲーム開始時（AI の生成完了後）にステータス表示が出す文言の先頭。</summary>
	private const string GameStartStatusPrefix = "ゲーム開始";

	/// <summary>
	/// ビギナーで 1 手打つと AI が応答し、手番が人間に戻ることを確認する。
	/// パス条件: AI が Python 連携（Rust / Python）であり、有効手を Invoke した後、盤面の石と表示スコアの合計がともに 6
	/// （初期 4 + 人間 1 + AI 1）になり、手番表示が「あなた（黒）」に戻って有効手が再表示されること。
	/// </summary>
	[UiFact]
	public void HumanMove_TriggersAiResponseAndReturnsTurnToHuman()
	{
		using var session = AppSession.Launch();
		StartBeginnerGame(session);

		// C# AI へのフォールバックでは WPF → Python の IPC を通らないため、この連携テストでは許容しない
		var badge = session.GetElement("AiEngineBadge").Name;
		Assert.True(AiBadge.IsPythonBacked(badge), $"AI が Python 連携ではありません（C# にフォールバックした可能性）: \"{badge}\"");

		PlayOneRound(session);
	}

	/// <summary>
	/// 着手と AI 応答の後に「戻す」を押すと、初期盤面に戻ることを確認する。
	/// パス条件: スコアが 2 対 2、中央 4 マスの色が初期配置、ほかが空き、有効手が 4 マスという初期盤面に戻り、
	/// 人間の手番表示が復元されること（人間と AI の 2 手分が戻る）。
	/// </summary>
	[UiFact]
	public void Undo_AfterAiResponds_RestoresInitialBoard()
	{
		using var session = AppSession.Launch();
		StartBeginnerGame(session);
		PlayOneRound(session);

		var undoButton = session.GetElement("UndoButton");
		UiWait.Until(() => undoButton.IsEnabled, () => "「戻す」ボタンが有効になりませんでした。");
		undoButton.Patterns.Invoke.Pattern.Invoke();

		WaitForInitialBoard(session);
	}

	/// <summary>
	/// 着手後に「新規ゲーム」を押すと、初期盤面に戻ることを確認する。
	/// パス条件: スコアが 2 対 2、中央 4 マスの色が初期配置、ほかが空き、有効手が 4 マスという初期盤面に戻り、
	/// 人間の手番表示が復元されること。
	/// </summary>
	[UiFact]
	public void NewGame_AfterMoves_RestoresInitialBoard()
	{
		using var session = AppSession.Launch();
		StartBeginnerGame(session);
		PlayOneRound(session);

		session.GetElement("NewGameButton").Patterns.Invoke.Pattern.Invoke();

		WaitForInitialBoard(session);
	}

	/// <summary>
	/// 初回の AI 初期化の完了を待ってから、難易度「ビギナー」を選び、その再生成の完了まで待つ。
	/// 初回初期化中に選ぶとノーマルで生成中の AI が使われ、再生成中は旧状態の盤面・手番が残るため着手がリセットされる。
	/// 再生成の完了は UI から見えないため、無効なマス（a1）を押してステータスを「ゲーム開始」以外にしておき、
	/// 選択後に「ゲーム開始」へ戻る（= AI 生成後の初期化が終わる）のを待って識別する。
	/// </summary>
	private static void StartBeginnerGame(AppSession session)
	{
		// 1. 初回の AI 初期化の完了（バッジ確定と初期盤面の表示）を待つ
		var badge = session.GetElement("AiEngineBadge");
		UiWait.Until(
			() => AiBadge.IsKnownEngine(badge.Name) && IsInitialBoard(session),
			() => $"初回の AI 初期化が完了しませんでした: バッジ=\"{badge.Name}\", スコア={DescribeScore(session)}");

		// 2. ステータスを「ゲーム開始」以外にしておく（a1 は初期盤面で無効手のため、盤面は変わらない）
		var status = session.GetElement("StatusMessageText");
		session.GetElement("a1").Patterns.Invoke.Pattern.Invoke();
		UiWait.Until(
			() => !status.Name.StartsWith(GameStartStatusPrefix),
			() => $"無効手クリックでステータスが変わりませんでした: \"{status.Name}\"");

		// 3. 難易度を選び、再生成後の「ゲーム開始」表示に戻るのを待つ
		SelectBeginner(session);
		UiWait.Until(
			() => status.Name.StartsWith(GameStartStatusPrefix),
			() => $"難易度変更後の AI 再生成が完了しませんでした: ステータス=\"{status.Name}\"");

		WaitForHumanTurn(session);
	}

	/// <summary>難易度 ComboBox から「ビギナー」を選ぶ（既定はノーマルで永続化されないため毎回選ぶ）。</summary>
	private static void SelectBeginner(AppSession session)
		=> session.GetElement("DifficultyComboBox").AsComboBox().Select(BeginnerIndex);

	/// <summary>人間の手番になり、有効手が表示されるまで待つ（AI 初期化・応答の完了を含む）。</summary>
	private static void WaitForHumanTurn(AppSession session)
		=> UiWait.Until(
			() => IsHumanTurn(session),
			() => $"人間の手番になりませんでした: 手番=\"{session.GetElement("CurrentPlayerText").Name}\", 有効手={BoardReader.ValidMoves(session).Count}");

	/// <summary>初期盤面（人間の手番）に戻るまで待つ。違いがあれば失敗メッセージに列挙する。</summary>
	private static void WaitForInitialBoard(AppSession session)
		=> UiWait.Until(
			() => IsInitialBoard(session) && IsHumanTurn(session),
			() => $"初期盤面に戻りませんでした: スコア={DescribeScore(session)}, 手番=\"{session.GetElement("CurrentPlayerText").Name}\", 差異=[{string.Join("; ", BoardReader.InitialPositionMismatches(session))}]");

	/// <summary>
	/// 人間が 1 手打ち、AI が応答して人間の手番に戻るまでを実行する。
	/// 事前条件: 人間の手番であること。事後条件: 盤面の石と表示スコアの合計がともに 6（初期 4 + 人間 1 + AI 1）で人間の手番。
	/// </summary>
	private static void PlayOneRound(AppSession session)
	{
		var firstMove = BoardReader.ValidMoves(session)[0];
		session.GetElement(firstMove).Patterns.Invoke.Pattern.Invoke();

		// AI の応答（Python 起動・探索）が終わり、人間の手番に戻るまで待つ。
		// 盤面の石だけでなく、スコア表示のバインディングが更新されたことも合計で確認する
		UiWait.Until(
			() => BoardReader.OccupiedSquares(session).Count == 6 && DisplayedStoneTotal(session) == 6 && IsHumanTurn(session),
			() => $"AI 応答後の状態になりませんでした: 石={BoardReader.OccupiedSquares(session).Count}, スコア={DescribeScore(session)}, 手番=\"{session.GetElement("CurrentPlayerText").Name}\"");
	}

	/// <summary>表示されている黒・白スコアの合計。数値として読めない場合は -1。</summary>
	private static int DisplayedStoneTotal(AppSession session)
		=> int.TryParse(session.GetElement("BlackScoreText").Name, out var black)
			&& int.TryParse(session.GetElement("WhiteScoreText").Name, out var white)
			? black + white
			: -1;

	/// <summary>失敗メッセージ用に、表示中の黒・白スコアを文字列にする。</summary>
	private static string DescribeScore(AppSession session)
		=> $"黒=\"{session.GetElement("BlackScoreText").Name}\", 白=\"{session.GetElement("WhiteScoreText").Name}\"";

	/// <summary>
	/// 人間が黒の初期盤面か。スコアが 2 対 2 で、中央 4 マスの色・ほかのマスの空き・有効手 4 マスがすべて一致していること。
	/// </summary>
	private static bool IsInitialBoard(AppSession session)
		=> session.GetElement("BlackScoreText").Name == "2"
			&& session.GetElement("WhiteScoreText").Name == "2"
			&& BoardReader.InitialPositionMismatches(session).Count == 0;

	/// <summary>手番表示が人間の手番になっており、有効手が表示されているか。</summary>
	private static bool IsHumanTurn(AppSession session)
		=> session.GetElement("CurrentPlayerText").Name.Contains(HumanTurnText) && BoardReader.ValidMoves(session).Count > 0;
}
