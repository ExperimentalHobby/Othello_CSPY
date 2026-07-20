using System.Collections.ObjectModel;
using System.Windows.Input;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Kifu;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// 棋譜再生ウィンドウ用 ViewModel。
/// KifuPlayer を通じて盤面を 1 手ずつ進めたり戻したりできる。
/// </summary>
public sealed class KifuViewModel : ViewModelBase
{
	private readonly KifuPlayer _player;

	public ObservableCollection<BoardSquareViewModel> BoardSquares { get; } = new();

	// --- 再生コントロールコマンド ---
	public ICommand StepForwardCommand { get; }
	public ICommand StepBackCommand { get; }
	public ICommand GoToStartCommand { get; }
	public ICommand GoToEndCommand { get; }

	// --- バッキングフィールド ---
	private int _currentMove;
	private bool _canStepForward;
	private bool _canStepBack;
	private string _statusMessage = string.Empty;

	/// <summary>現在表示している手数（0 = 初期盤面）</summary>
	public int CurrentMove
	{
		get => _currentMove;
		private set => SetProperty(ref _currentMove, value);
	}

	/// <summary>棋譜の総手数</summary>
	public int TotalMoves => _player.TotalMoves;

	/// <summary>次の手に進めるかどうか</summary>
	public bool CanStepForward
	{
		get => _canStepForward;
		private set => SetProperty(ref _canStepForward, value);
	}

	/// <summary>前の手に戻れるかどうか</summary>
	public bool CanStepBack
	{
		get => _canStepBack;
		private set => SetProperty(ref _canStepBack, value);
	}

	/// <summary>手数・直前の着手情報などを表示するステータス文字列</summary>
	public string StatusMessage
	{
		get => _statusMessage;
		private set => SetProperty(ref _statusMessage, value);
	}

	/// <summary>対局日時・難易度・勝敗などの棋譜メタ情報</summary>
	public string KifuInfo { get; }

	/// <summary>KifuPlayer と棋譜メタを受け取って再生 ViewModel を初期化する。</summary>
	public KifuViewModel(KifuPlayer player, KifuRecord? record = null)
	{
		_player = player;
		KifuInfo = BuildKifuInfo(record);

		for (var r = 0; r < 8; r++)
			for (var c = 0; c < 8; c++)
				BoardSquares.Add(new BoardSquareViewModel(new Position(r, c)));

		StepForwardCommand = new RelayCommand(() => DoStep(forward: true));
		StepBackCommand = new RelayCommand(() => DoStep(forward: false));
		GoToStartCommand = new RelayCommand(() => { _player.GoToStart(); Refresh(); });
		GoToEndCommand = new RelayCommand(() => { _player.GoToEnd(); Refresh(); });

		Refresh();
	}

	private void DoStep(bool forward)
	{
		if (forward) _player.StepForward();
		else _player.StepBack();
		Refresh();
	}

	private void Refresh()
	{
		CurrentMove = _player.CurrentMoveIndex;
		CanStepForward = _player.CanStepForward;
		CanStepBack = _player.CanStepBack;

		var board = _player.CurrentBoard;
		for (var r = 0; r < 8; r++)
			for (var c = 0; c < 8; c++)
				BoardSquares[r * 8 + c].SetPiece(board.GetPiece(r, c));

		StatusMessage = BuildStatusMessage();
	}

	private string BuildStatusMessage()
	{
		if (CurrentMove == 0)
			return "初期盤面";
		var move = _player.LastExecutedMove;
		if (move is null)
			return $"{CurrentMove} / {TotalMoves} 手目";
		if (move.IsPass)
			return $"{CurrentMove} / {TotalMoves} 手目  {move.Player.ToDisplayString()} パス";
		var notation = new Position(move.Row!.Value, move.Col!.Value).ToNotation();
		return $"{CurrentMove} / {TotalMoves} 手目  {move.Player.ToDisplayString()} {notation}";
	}

	private static string BuildKifuInfo(KifuRecord? record)
	{
		if (record is null)
			return string.Empty;

		var date = record.PlayedAt.LocalDateTime.ToString("yyyy/MM/dd HH:mm");
		var diff = record.Difficulty.ToDisplayString();
		var result = record.Result is null
			? "引き分け"
			: record.Mode == GameMode.CpuVsCpu
				? $"{record.Result.Value.ToDisplayString()}の勝利"
				: (record.Result == record.HumanColor ? "あなたの勝利" : "AI の勝利");

		return $"{date}  難易度:{diff}  {result}  (黒:{record.FinalScore.Black} 白:{record.FinalScore.White})";
	}
}
