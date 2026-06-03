// オセロ コンソール版エントリポイント
// 人間 vs Python AI の対局をターミナル上で行う。
// 入力形式: "d4"（列文字 + 行番号）または "3 4"（行インデックス スペース 列インデックス）

using Technopro.Othello.Core.AI;
using Technopro.Othello.Core.Game;
using Technopro.Othello.Core.Models;

// 日本語・記号を正しく表示するために UTF-8 を設定する
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding  = System.Text.Encoding.UTF8;

Console.WriteLine("=== オセロ (Python AI 対戦) ===");
Console.WriteLine();

// 難易度と人間の色を対話式で選択する
DifficultyLevel difficulty  = SelectDifficulty();
PlayerColor     humanColor  = SelectHumanColor();
PlayerColor     aiColor     = humanColor.Opponent();

Console.WriteLine();
Console.WriteLine($"設定: あなた = {humanColor.ToDisplayString()}, AI = {aiColor.ToDisplayString()}, 難易度 = {difficulty.ToDisplayString()}");
Console.WriteLine();

// 実行ディレクトリの Othello.Python/ai.py を使用する
string scriptPath = Path.Combine(AppContext.BaseDirectory, "Othello.Python", "ai.py");
if (!File.Exists(scriptPath))
{
    // スクリプトが見つからない場合はエラーを表示して終了する
    Console.Error.WriteLine($"エラー: Python スクリプトが見つかりません: {scriptPath}");
    return;
}

// using により Dispose が確実に呼ばれ Python プロセスが終了する
using var ai     = new PythonSubprocessAI(difficulty, scriptPath);
var       engine = new GameEngine();
engine.Initialize();

// ゲームループ: どちらかのプレイヤーが着手できなくなるまで繰り返す
while (engine.GameState.IsGameInProgress())
{
    PrintBoard(engine.CurrentBoard, engine);
    Console.WriteLine();

    if (engine.CurrentPlayer == humanColor)
    {
        // --- 人間のターン ---
        var validMoves = engine.GetValidMoves(humanColor);
        if (validMoves.Count == 0)
        {
            // 有効手がない → 強制パス
            Console.WriteLine("有効な手がないためパスします...");
            engine.Pass();
            continue;
        }

        // 人間の入力を受け取り着手する（入力エラー時は再入力を促す）
        Position? move = GetHumanMove(engine, humanColor, validMoves);
        if (move.HasValue)
        {
            var result = engine.MakeMove(move.Value);
            if (!result.IsSuccess)
                Console.WriteLine($"無効な手: {result.Message}");
        }
    }
    else
    {
        // --- AI のターン ---
        Console.Write($"AI（{aiColor.ToDisplayString()}）が考え中...");
        var validMoves = engine.GetValidMoves(aiColor);
        if (validMoves.Count == 0)
        {
            // AI に有効手がない → パス
            Console.WriteLine("パス");
            engine.Pass();
            continue;
        }

        // Python AI に盤面を送信して最善手を受け取る
        var bestMove = ai.GetBestMove(engine.CurrentBoard, aiColor);
        Console.WriteLine($" → {ColChar(bestMove.Column)}{bestMove.Row + 1}");
        engine.MakeMove(bestMove);
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
    Console.WriteLine("  1. イージー");
    Console.WriteLine("  2. ノーマル");
    Console.WriteLine("  3. ハード");
    Console.Write("選択 [1-3, デフォルト: 2]: ");

    return Console.ReadLine()?.Trim() switch
    {
        "1" => DifficultyLevel.Easy,
        "3" => DifficultyLevel.Hard,
        _   => DifficultyLevel.Medium  // 2 または不正入力はノーマルに統一する
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
        _   => PlayerColor.Black  // 1 または不正入力は黒に統一する
    };
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
                _                 => " ·" // Empty
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

        Position? pos = ParseInput(input);
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

/// <summary>
/// 文字列入力を Position に変換する。
/// "d4" 形式（列文字 a-h + 行番号 1-8）または "3 4" 形式（行列インデックス 0-7）をサポートする。
/// </summary>
/// <param name="input">変換対象の文字列（小文字変換済み）</param>
/// <returns>有効な座標であれば Position、不正入力であれば null</returns>
static Position? ParseInput(string input)
{
    try
    {
        // "d4" 形式: 先頭が文字、2 文字目が数字の場合
        if (input.Length == 2 && char.IsLetter(input[0]) && char.IsDigit(input[1]))
        {
            int col = input[0] - 'a'; // 'a'=0, 'b'=1, ... 'h'=7
            int row = input[1] - '1'; // '1'=0, '2'=1, ... '8'=7
            if (Position.IsValid(row, col))
                return new Position(row, col);
        }

        // "3 4" 形式: スペース区切りで 2 つの整数
        var parts = input.Split(' ');
        if (parts.Length == 2 && int.TryParse(parts[0], out int r) && int.TryParse(parts[1], out int c))
        {
            if (Position.IsValid(r, c))
                return new Position(r, c);
        }
    }
    catch
    {
        // 例外はすべて null 返却で吸収する
    }

    return null;
}

/// <summary>
/// 列インデックス（0-7）を対応する列文字（'a'-'h'）に変換する。
/// </summary>
/// <param name="col">列インデックス（0=a, 7=h）</param>
/// <returns>対応する列文字</returns>
static char ColChar(int col) => (char)('a' + col);
