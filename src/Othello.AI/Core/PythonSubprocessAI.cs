namespace Technopro.Othello.Core.AI;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Technopro.Othello.Core.Models;

/// <summary>
/// Python サブプロセスと JSON / stdin-stdout で通信する AI 実装。
/// IAIStrategy を実装し、GetBestMove が呼ばれるたびに盤面情報を JSON で送信し、
/// Python 側のアルファベータ探索結果（着手座標）を受信して返す。
///
/// IPC プロトコル:
///   C# → Python (stdin): {"board": [[int,...]], "player": int, "depth": int}
///   Python → C# (stdout): {"row": int, "col": int}  or  {"error": string}
///
/// プロセスのライフサイクル:
///   コンストラクタで Python プロセスを起動し、Dispose で終了させる。
///   1 ゲームにつき 1 プロセスを維持することで Python 起動コストを分散する。
/// </summary>
public sealed class PythonSubprocessAI : IAIStrategy, IDisposable
{
    /// <summary>Python AI からの応答を待つ最大時間（ミリ秒）。Hard 難易度は時間がかかるため十分な余裕を持つ。</summary>
    private const int AiResponseTimeoutMs = 60_000;
    /// <summary>Python 実行可能ファイルの候補を検出する際のプロセス起動タイムアウト（ミリ秒）。</summary>
    private const int PythonProbeTimeoutMs = 3_000;

    /// <summary>起動した Python プロセスの参照</summary>
    private readonly Process _process;

    /// <summary>
    /// Python プロセスの標準エラー出力を非同期で蓄積するバッファ。
    /// 稼働中に stderr をドレインし続けることでパイプ詰まりによるデッドロックを防ぐ。
    /// </summary>
    private readonly StringBuilder _stderrBuffer = new();

    /// <summary>Dispose 後の多重呼び出しを防ぐフラグ</summary>
    private bool _disposed;

    /// <summary>この AI インスタンスの難易度（探索深さの決定に使用）</summary>
    public DifficultyLevel Difficulty { get; }

    /// <summary>UI 表示用バックエンド名。Rust / Python を構築時に一度だけ判定しキャッシュする。</summary>
    public string EngineName { get; }

    /// <summary>
    /// 指定した難易度とスクリプトパスで Python AI プロセスを起動する。
    /// </summary>
    /// <param name="difficulty">AI の難易度（探索深さに影響）</param>
    /// <param name="pythonScriptPath">ai.py の絶対パス</param>
    /// <exception cref="FileNotFoundException">ai.py が指定パスに存在しない場合</exception>
    /// <exception cref="InvalidOperationException">Python が見つからない場合</exception>
    public PythonSubprocessAI(DifficultyLevel difficulty, string pythonScriptPath)
    {
        Difficulty = difficulty;
        // バックグラウンドスレッド（Task.Run）から呼ばれる前提で IsRustAvailable を確定させる。
        // これにより Lazy の初期化が UI スレッドではなくここで行われる。
        EngineName = AiScriptPaths.IsRustAvailable ? "AI: Rust" : "AI: Python";

        // スクリプトの存在を事前確認し、見つからない場合は明確なエラーを出す
        if (!File.Exists(pythonScriptPath))
            throw new FileNotFoundException(
                $"Python スクリプトが見つかりません: {pythonScriptPath}\n" +
                "dotnet build を実行してファイルが出力ディレクトリにコピーされているか確認してください。");

        // OS に合わせた Python 実行ファイルを自動検出する
        string pythonExe = FindPythonExecutable();

        // スクリプトと同じディレクトリを作業ディレクトリにすることで
        // ai.py が board.py / evaluator.py / alpha_beta.py を import できるようにする
        string scriptDir = Path.GetDirectoryName(pythonScriptPath)!;

        // BOM なし UTF-8 エンコーディング。
        // Encoding.UTF8 は BOM 付きのため、Python 側で json.loads が
        // "Expecting value: line 1 column 1 (char 0)" で失敗する原因となる。
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var startInfo = new ProcessStartInfo
        {
            FileName        = pythonExe,
            // -u: stdout/stdin をアンバッファードにして応答遅延を防ぐ
            Arguments       = $"-u \"{pythonScriptPath}\"",
            WorkingDirectory = scriptDir,          // import 解決に必要
            UseShellExecute = false,
            RedirectStandardInput  = true,         // C# から Python へ JSON を送る
            RedirectStandardOutput = true,         // Python から C# へ結果を受け取る
            RedirectStandardError  = true,         // Python の例外スタックをキャプチャする
            CreateNoWindow  = true,
            StandardInputEncoding  = utf8NoBom,   // BOM なし: Python の json.loads を正常動作させる
            StandardOutputEncoding = utf8NoBom,
            StandardErrorEncoding  = utf8NoBom,
        };
        // Python の I/O エンコーディングを明示的に UTF-8 に設定する
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        _process = new Process { StartInfo = startInfo };

        // stderr を非同期で読み続けてバッファに蓄積する。
        // 同期 ReadToEnd と異なり、稼働中でもパイプを詰まらせずにエラー出力を回収できる。
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (_stderrBuffer)
                _stderrBuffer.AppendLine(e.Data);
        };

        _process.Start();
        try
        {
            _process.BeginErrorReadLine();
        }
        catch
        {
            try { _process.Kill(); } catch { /* Kill 失敗は無視 */ }
            _process.Dispose();
            _process = null!;
            throw;
        }
    }

    /// <summary>
    /// 盤面と手番を Python AI に送信し、最善手の座標を返す。
    /// Python プロセスが応答しない場合は stderr の内容を含む例外を投げる。
    /// </summary>
    /// <param name="board">現在の盤面（変更しない）</param>
    /// <param name="playerColor">AI が担当するプレイヤーの色</param>
    /// <returns>AI が選択した着手先の Position</returns>
    /// <exception cref="ObjectDisposedException">Dispose 済みのインスタンスを呼び出した場合</exception>
    /// <exception cref="InvalidOperationException">Python プロセスが応答しない、またはエラーを返した場合</exception>
    public Position GetBestMove(Board board, PlayerColor playerColor)
    {
        // Dispose 後の呼び出しを早期に弾く
        ObjectDisposedException.ThrowIf(_disposed, this);

        // プロセスが既に終了していれば通信不可
        if (_process.HasExited)
            throw new InvalidOperationException(ReadProcessError("Python プロセスが予期せず終了しました"));

        // 盤面・手番・探索深さを JSON にシリアライズして送信する
        // time_ms: Hard 難易度のみ反復深化用の時間制限を付加する。null の場合は固定深さ探索
        var request = new
        {
            board   = SerializeBoard(board),
            player  = (int)playerColor,              // 1=黒, 2=白
            depth   = Difficulty.GetSearchDepth(),   // 難易度に応じた探索深さ
            time_ms = Difficulty.GetTimeLimitMs()    // Hard=8000, Easy/Normal=null
        };

        string json = JsonSerializer.Serialize(request);
        _process.StandardInput.WriteLine(json);   // 改行を区切り記号として使用
        _process.StandardInput.Flush();            // バッファをフラッシュして即座に Python 側へ届ける

        // Python 側から 1 行（JSON）を読む（タイムアウト付き）
        // ReadLine() はプロセスが応答しない場合に無限ブロックするため、スレッドで制限する
        string? response = ReadLineWithTimeout(timeoutMs: AiResponseTimeoutMs);
        if (response == null)
            throw new InvalidOperationException(ReadProcessError("Python AI プロセスが応答しませんでした"));

        // JSON を解析して着手座標を取り出す
        using var doc = JsonDocument.Parse(response);

        // Python 側でエラーが発生した場合はその内容を例外として投げる
        if (doc.RootElement.TryGetProperty("error", out var errorProp))
            throw new InvalidOperationException($"Python AI エラー: {errorProp.GetString()}");

        int row = doc.RootElement.GetProperty("row").GetInt32();
        int col = doc.RootElement.GetProperty("col").GetInt32();
        return new Position(row, col);
    }

    /// <summary>
    /// 指定したタイムアウト時間内に StandardOutput から 1 行読み取る。
    /// タイムアウトした場合は Python プロセスを強制終了して TimeoutException を投げる。
    /// </summary>
    /// <param name="timeoutMs">タイムアウトのミリ秒数</param>
    /// <returns>読み取った行（プロセス終了でストリームが閉じた場合は null）</returns>
    /// <exception cref="TimeoutException">指定時間内に応答がなかった場合</exception>
    private string? ReadLineWithTimeout(int timeoutMs)
    {
        // ReadLineAsync を使い、専用スレッドを生成せずにタイムアウトを掛ける。
        // この呼び出し自体は Task.Run 上のスレッドプールスレッドで実行されるため、Wait によるブロックは許容する。
        var readTask = _process.StandardOutput.ReadLineAsync();

        bool completed;
        try
        {
            completed = readTask.Wait(timeoutMs);
        }
        catch (AggregateException ae)
        {
            // 読み取り中に発生した例外は元のスタックトレースを保持して再スローする
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ae.InnerException ?? ae).Throw();
            throw; // 到達しない（コンパイラの確定代入を満たすため）
        }

        if (!completed)
        {
            // タイムアウト: Python プロセスを強制終了する
            try { if (!_process.HasExited) _process.Kill(); } catch { }
            throw new TimeoutException(
                $"Python AI が {timeoutMs / 1000} 秒以内に応答しませんでした。Hard 難易度では数秒かかる場合があります。");
        }

        return readTask.Result;
    }

    /// <summary>
    /// 非同期で蓄積した標準エラー出力をエラーメッセージに付加する。
    /// BeginErrorReadLine により稼働中・終了後を問わず安全に内容を参照できる。
    /// </summary>
    /// <param name="baseMessage">基本エラーメッセージ</param>
    /// <returns>stderr の内容を付加したエラーメッセージ文字列</returns>
    private string ReadProcessError(string baseMessage)
    {
        string stderr;
        lock (_stderrBuffer)
            stderr = _stderrBuffer.ToString().Trim();

        return string.IsNullOrEmpty(stderr)
            ? baseMessage
            : $"{baseMessage}\n--- Python エラー ---\n{stderr}";
    }

    /// <summary>
    /// Board の内部状態を JSON シリアライズ可能な int[8][8] 配列に変換する。
    /// 値の対応: 0=Empty, 1=Black, 2=White（PlayerColor の int 値と一致）。
    /// </summary>
    /// <param name="board">変換元の盤面</param>
    /// <returns>8×8 の int 配列（行優先）</returns>
    internal static int[][] SerializeBoard(Board board)
    {
        var grid = new int[Board.BoardSize][];
        for (int r = 0; r < Board.BoardSize; r++)
        {
            grid[r] = new int[Board.BoardSize];
            for (int c = 0; c < Board.BoardSize; c++)
                grid[r][c] = (int)board.GetPiece(r, c);
        }
        return grid;
    }

    /// <summary>
    /// OS に応じて Python 実行ファイル名を自動検出する。
    /// Windows では py → python3 → python の順で試す。
    /// 非 Windows では python3 → python の順で試す。
    /// </summary>
    /// <returns>使用可能な Python 実行ファイル名</returns>
    /// <exception cref="InvalidOperationException">Python 3.8 以上が見つからない場合</exception>
    private static string FindPythonExecutable()
    {
        // Windows では py（Windows Python Launcher）が最も確実
        string[] candidates = OperatingSystem.IsWindows()
            ? ["py", "python3", "python"]
            : ["python3", "python"];

        foreach (var exe in candidates)
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow = true
                });

                if (probe == null) continue;

                bool exited = probe.WaitForExit(PythonProbeTimeoutMs);

                if (!exited)
                {
                    // タイムアウト: プロセスを強制終了して次の候補へ
                    try { probe.Kill(); } catch { }
                    continue;
                }

                if (probe.ExitCode == 0)
                    return exe;
            }
            catch
            {
                // 実行ファイルが存在しない場合など → 次の候補へ
            }
        }
        throw new InvalidOperationException(
            "Python 3.8 以上が見つかりません。インストールされているか確認してください。");
    }

    /// <summary>
    /// Python プロセスを終了し、関連リソースを解放する。
    /// 二重呼び出しは安全に無視する。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // stdin を閉じることで Python の for line in sys.stdin ループを終了させる
        try { _process.StandardInput.Close(); } catch { }

        // プロセスがまだ生きている場合は強制終了する
        try { if (!_process.HasExited) _process.Kill(); } catch { }

        _process.Dispose();
    }
}
