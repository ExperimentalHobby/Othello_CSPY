namespace Technopro.Othello.Core.AI;

/// <summary>
/// Python AI スクリプトのパスを一元管理するクラス。
/// GameViewModel・Console Program など複数の呼び出し元が同じパス解決ロジックを共有する。
/// </summary>
public static class AiScriptPaths
{
    /// <summary>
    /// 実行ディレクトリ配下の Othello.Python/ai.py への絶対パス。
    /// Python スクリプトは各プロジェクトの Content ビルドアクションで出力ディレクトリにコピーされる。
    /// </summary>
    public static string AiScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "Othello.Python", "ai.py");

    /// <summary>
    /// Rust 拡張モジュール（othello_ai_rust.pyd / .so）が出力ディレクトリに存在するかどうか。
    /// true の場合、Python AI は Rust バックエンドで動作する。
    /// </summary>
    public static bool IsRustAvailable
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Othello.Python");
            if (!Directory.Exists(dir))
                return false;
            return Directory.EnumerateFiles(dir, "othello_ai_rust.*")
                .Any(f => f.EndsWith(".pyd", StringComparison.OrdinalIgnoreCase)
                       || f.EndsWith(".so",  StringComparison.OrdinalIgnoreCase));
        }
    }
}
