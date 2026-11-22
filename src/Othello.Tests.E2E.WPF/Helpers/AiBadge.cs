namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>メニューバーの AI エンジン表示（AiEngineBadge）の判定ヘルパー。</summary>
public static class AiBadge
{
	/// <summary>表示が実エンジン名（Rust / Python / C#）に確定しているか。起動直後の "AI: ?" は false。</summary>
	public static bool IsKnownEngine(string label)
		=> IsPythonBacked(label) || label.Contains("AI: C#");

	/// <summary>
	/// Python プロセス経由（Rust または Python）の AI か。
	/// Python の検出・起動に失敗すると C# AI にフォールバックするため、WPF → Python の IPC を検証するテストはこれで区別する。
	/// </summary>
	public static bool IsPythonBacked(string label)
		=> label.Contains("AI: Rust") || label.Contains("AI: Python");
}
