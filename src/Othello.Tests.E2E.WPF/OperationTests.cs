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

	/// <summary>
	/// ビギナーで 1 手打つと AI が応答し、手番が人間に戻ることを確認する。
	/// パス条件: 有効手を Invoke した後、石の合計が 6（初期 4 + 人間 1 + AI 1）になり、
	/// 手番表示が「あなた（黒）」に戻って有効手が再表示されること。
	/// </summary>
	[UiFact]
	public void HumanMove_TriggersAiResponseAndReturnsTurnToHuman()
	{
		using var session = AppSession.Launch();
		SelectBeginner(session);
		WaitForHumanTurn(session);

		PlayOneRound(session);
	}

	/// <summary>
	/// 着手と AI 応答の後に「戻す」を押すと、初期盤面に戻ることを確認する。
	/// パス条件: 黒・白のスコアがともに 2 に戻り、人間の手番表示と有効手が復元されること（人間と AI の 2 手分が戻る）。
	/// </summary>
	[UiFact]
	public void Undo_AfterAiResponds_RestoresInitialBoard()
	{
		using var session = AppSession.Launch();
		SelectBeginner(session);
		WaitForHumanTurn(session);
		PlayOneRound(session);

		var undoButton = session.GetElement("UndoButton");
		UiWait.Until(() => undoButton.IsEnabled, () => "「戻す」ボタンが有効になりませんでした。");
		undoButton.Patterns.Invoke.Pattern.Invoke();

		UiWait.Until(
			() => IsInitialBoard(session) && IsHumanTurn(session),
			() => $"初期盤面に戻りませんでした: 黒=\"{session.GetElement("BlackScoreText").Name}\", 白=\"{session.GetElement("WhiteScoreText").Name}\", 石={BoardReader.OccupiedSquares(session).Count}");
	}

	/// <summary>
	/// 着手後に「新規ゲーム」を押すと、初期盤面に戻ることを確認する。
	/// パス条件: 黒・白のスコアがともに 2 に戻り、石が中央 4 マスだけになり、人間の手番表示と有効手が復元されること。
	/// </summary>
	[UiFact]
	public void NewGame_AfterMoves_RestoresInitialBoard()
	{
		using var session = AppSession.Launch();
		SelectBeginner(session);
		WaitForHumanTurn(session);
		PlayOneRound(session);

		session.GetElement("NewGameButton").Patterns.Invoke.Pattern.Invoke();

		UiWait.Until(
			() => IsInitialBoard(session) && IsHumanTurn(session),
			() => $"初期盤面に戻りませんでした: 黒=\"{session.GetElement("BlackScoreText").Name}\", 白=\"{session.GetElement("WhiteScoreText").Name}\", 石={BoardReader.OccupiedSquares(session).Count}");
		Assert.Equal(new[] { "d4", "e4", "d5", "e5" }.Order(), BoardReader.OccupiedSquares(session).Order());
	}

	/// <summary>難易度 ComboBox から「ビギナー」を選ぶ（既定はノーマルで永続化されないため毎回選ぶ）。</summary>
	private static void SelectBeginner(AppSession session)
		=> session.GetElement("DifficultyComboBox").AsComboBox().Select(BeginnerIndex);

	/// <summary>人間の手番になり、有効手が表示されるまで待つ（AI 初期化・応答の完了を含む）。</summary>
	private static void WaitForHumanTurn(AppSession session)
		=> UiWait.Until(
			() => IsHumanTurn(session),
			() => $"人間の手番になりませんでした: 手番=\"{session.GetElement("CurrentPlayerText").Name}\", 有効手={BoardReader.ValidMoves(session).Count}");

	/// <summary>
	/// 人間が 1 手打ち、AI が応答して人間の手番に戻るまでを実行する。
	/// 事前条件: 人間の手番であること。事後条件: 石の合計が 6（初期 4 + 人間 1 + AI 1）で人間の手番。
	/// </summary>
	private static void PlayOneRound(AppSession session)
	{
		var firstMove = BoardReader.ValidMoves(session)[0];
		session.GetElement(firstMove).Patterns.Invoke.Pattern.Invoke();

		// AI の応答（Python 起動・探索）が終わり、人間の手番に戻るまで待つ
		UiWait.Until(
			() => BoardReader.OccupiedSquares(session).Count == 6 && IsHumanTurn(session),
			() => $"AI 応答後の状態になりませんでした: 石={BoardReader.OccupiedSquares(session).Count}, 手番=\"{session.GetElement("CurrentPlayerText").Name}\"");
	}

	/// <summary>スコアが 2 対 2 の初期盤面か。</summary>
	private static bool IsInitialBoard(AppSession session)
		=> session.GetElement("BlackScoreText").Name == "2" && session.GetElement("WhiteScoreText").Name == "2";

	/// <summary>手番表示が人間の手番になっており、有効手が表示されているか。</summary>
	private static bool IsHumanTurn(AppSession session)
		=> session.GetElement("CurrentPlayerText").Name.Contains(HumanTurnText) && BoardReader.ValidMoves(session).Count > 0;
}
