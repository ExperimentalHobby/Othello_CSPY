namespace Technopro.Othello.Core.AI;

/// <summary>
/// Python AI スクリプトのパスを一元管理するクラス。
/// GameViewModel・Console Program など複数の呼び出し元が同じパス解決ロジックを共有する。
/// </summary>
public static class AiScriptPaths
{
    /// <summary>
    /// 実行ディレクトリ配下の Othello.AI/Python/ai.py への絶対パス。
    /// Python スクリプトは各プロジェクトの Content ビルドアクションで出力ディレクトリにコピーされる。
    /// </summary>
    public static string AiScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "Othello.AI", "Python", "ai.py");
}
