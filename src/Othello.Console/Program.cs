// オセロ コンソール版エントリポイント
// 人間 vs Python AI の対局をターミナル上で行う。
// 入力形式: "d4"（列文字 + 行番号）または "3 4"（行インデックス スペース 列インデックス）

using Technopro.Othello.Console;
using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

// 日本語・記号を正しく表示するために UTF-8 を設定する
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=== オセロ (Python AI 対戦) ===");
Console.WriteLine();

// 難易度と人間の色を対話式で選択する
DifficultyLevel difficulty = SelectDifficulty();
PlayerColor humanColor = SelectHumanColor();
PlayerColor aiColor = humanColor.Opponent();

Console.WriteLine();
Console.WriteLine($"設定: あなた = {humanColor.ToDisplayString()}, AI = {aiColor.ToDisplayString()}, 難易度 = {difficulty.ToDisplayString()}");
Console.WriteLine();

// AiScriptPaths でパスを一元管理する（GameViewModel と同じパス解決ロジック）
string scriptPath = AiScriptPaths.AiScriptPath;
if (!File.Exists(scriptPath))
{
	// スクリプトが見つからない場合はエラーを表示して終了する
	Console.Error.WriteLine($"エラー: Python スクリプトが見つかりません: {scriptPath}");
	return;
}

// using により Dispose が確実に呼ばれ Python プロセスが終了する
using var ai = new PythonSubprocessAI(difficulty, scriptPath);
var engine = new GameEngine();
engine.Initialize();

// ゲームループ: どちらかのプレイヤーが着手できなくなるまで繰り返す
while (engine.GameState.IsGameInProgress())
{
	PrintBoard(engine.CurrentBoard, engine);
	Console.WriteLine();

	if (engine.CurrentPlayer == humanColor)
	{
		// --- 人間のターン ---
		// GameEngine は有効手のないプレイヤーを手番に残さず自動でスキップするため、
		// ここでは humanColor に必ず有効手がある（明示的なパス処理は不要）。
		var validMoves = engine.GetValidMoves(humanColor);

		// 人間の入力を受け取り着手する（入力エラー時は再入力を促す）
		Position? move = GetHumanMove(engine, humanColor, validMoves);
		if (move.HasValue)
		{
			var result = engine.MakeMove(move.Value);
			if (!result.IsSuccess)
				Console.WriteLine($"無効な手: {result.Message}");
			else
				PrintPassNoticeIfAny(engine);
		}
	}
	else
	{
		// --- AI のターン ---
		Console.Write($"AI（{aiColor.ToDisplayString()}）が考え中...");

		// Python AI に盤面を送信して最善手を受け取る
		Position bestMove;
		try
		{
			bestMove = ai.GetBestMove(engine.CurrentBoard, aiColor);
		}
		catch (Exception ex)
		{
			Console.WriteLine();
			Console.Error.WriteLine($"AI エラー: {ex.Message}");
			Console.Error.WriteLine("ゲームを終了します。");
			return;
		}
		Console.WriteLine($" → {bestMove.ToNotation()}");
		var aiMoveResult = engine.MakeMove(bestMove);
		if (!aiMoveResult.IsSuccess)
		{
			Console.Error.WriteLine($"AI の手が無効でした: {aiMoveResult.Message}");
			return;
		}
		PrintPassNoticeIfAny(engine);
	}
}

// --- ゲーム終了 ---
PrintBoard(engine.CurrentBoard, engine);
var (winner, blackScore, whiteScore) = engine.GetResult();
Console.WriteLine();
Console.WriteLine("=== ゲーム終了 ===");
Console.WriteLine($"黒: {blackScore}  白: {whiteScore}");

// 勝敗を表示する（null は引き分けを意味する）
if (winner == null)
	Console.WriteLine("引き分け!");
else if (winner == humanColor)
	Console.WriteLine("あなたの勝利!");
else
	Console.WriteLine("AI の勝利!");

// --------------- ローカル関数 ---------------

/// <summary>
/// 難易度を対話式で選択させ、DifficultyLevel を返す。
/// 不正入力の場合はデフォルト（Medium）を返す。
/// </summary>
/// <returns>選択された DifficultyLevel</returns>
static DifficultyLevel SelectDifficulty()
{
	Console.WriteLine("難易度を選択してください:");
	Console.WriteLine("  0. ビギナー");
	Console.WriteLine("  1. イージー");
	Console.WriteLine("  2. ノーマル");
	Console.WriteLine("  3. ハード");
	Console.WriteLine("  4. エキスパート");
	Console.Write("選択 [0-4, デフォルト: 2]: ");

	return Console.ReadLine()?.Trim() switch
	{
		"0" => DifficultyLevel.Beginner,
		"1" => DifficultyLevel.Easy,
		"3" => DifficultyLevel.Hard,
		"4" => DifficultyLevel.Expert,
		_ => DifficultyLevel.Medium  // 2 または不正入力はノーマルに統一する
	};
}

/// <summary>
/// 人間が担当する色を対話式で選択させ、PlayerColor を返す。
/// 不正入力の場合はデフォルト（Black）を返す。
/// </summary>
/// <returns>人間が担当する PlayerColor（Black または White）</returns>
static PlayerColor SelectHumanColor()
{
	Console.WriteLine("あなたの色を選択してください:");
	Console.WriteLine("  1. 黒（先手）");
	Console.WriteLine("  2. 白（後手）");
	Console.Write("選択 [1-2, デフォルト: 1]: ");

	return Console.ReadLine()?.Trim() switch
	{
		"2" => PlayerColor.White,
		_ => PlayerColor.Black  // 1 または不正入力は黒に統一する
	};
}

/// <summary>
/// 直前のターン遷移で強制パスが発生していたら、その旨を表示する。
/// GameEngine が自動スキップしたパスをプレイヤーに通知するために使用する。
/// </summary>
/// <param name="engine">パス情報を取得するゲームエンジン</param>
static void PrintPassNoticeIfAny(GameEngine engine)
{
	if (engine.LastPassedPlayer is { } passed)
		Console.WriteLine($"{passed.ToDisplayString()} は打てる場所がないためパスしました");
}

/// <summary>
/// 盤面をテキスト形式でコンソールに出力する。
/// 黒=●、白=○、空=· で表示し、最下行にスコアを表示する。
/// </summary>
/// <param name="board">表示する盤面</param>
/// <param name="engine">スコア取得のために使用するゲームエンジン</param>
static void PrintBoard(Board board, GameEngine engine)
{
	// 列ヘッダー（a〜h）を出力する
	Console.WriteLine("    a  b  c  d  e  f  g  h");
	Console.WriteLine("  +------------------------+");

	for (int r = 0; r < 8; r++)
	{
		Console.Write($"{r + 1} |"); // 行番号（1〜8）を左端に表示する
		for (int c = 0; c < 8; c++)
		{
			// 石の種類に応じて記号を切り替える
			string sym = board.GetPiece(r, c) switch
			{
				PlayerColor.Black => " ●",
				PlayerColor.White => " ○",
				_ => " ·" // Empty
			};
			Console.Write(sym);
		}
		Console.WriteLine(" |");
	}

	Console.WriteLine("  +------------------------+");
	Console.WriteLine($"  黒: {engine.BlackScore}  白: {engine.WhiteScore}");
}

/// <summary>
/// 人間プレイヤーからの入力を受け取り、有効な着手座標を返す。
/// "undo" と入力された場合は Undo を実行して null を返す。
/// 入力が無効または有効手でない場合は再入力を促す。
/// </summary>
/// <param name="engine">Undo 処理のために使用するゲームエンジン</param>
/// <param name="humanColor">人間の担当色（ターン表示用）</param>
/// <param name="validMoves">現在の有効な着手位置のリスト</param>
/// <returns>有効な着手 Position、または Undo 実行時は null</returns>
static Position? GetHumanMove(GameEngine engine, PlayerColor humanColor, List<Position> validMoves)
{
	while (true)
	{
		string turnLabel = humanColor.ToDisplayString();
		Console.Write($"あなたの手（{turnLabel}、例: d4）: ");
		string? input = Console.ReadLine()?.Trim().ToLower();

		// 空入力は無視して再入力を促す
		if (string.IsNullOrEmpty(input))
			continue;

		if (input == "undo")
		{
			// 1 手取り消す（AI のターンになった場合はさらに 1 手取り消す）
			engine.Undo();
			if (engine.CurrentPlayer != humanColor)
				engine.Undo();
			return null; // null を返すことで呼び出し元のループを継続させる
		}

		Position? pos = ConsoleInputParser.ParseInput(input);
		if (pos == null)
		{
			// 入力形式が不正
			Console.WriteLine("入力形式が正しくありません (例: d4 または 3 4)");
			continue;
		}

		if (!validMoves.Contains(pos.Value))
		{
			// そのマスには有効手がない
			Console.WriteLine("そこには置けません。有効な手を選んでください。");
			continue;
		}

		return pos;
	}
}

