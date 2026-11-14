namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

/// <summary>
/// 実アプリのウィンドウを起動する GUI テスト用の Fact 属性。
/// 環境変数 <see cref="EnableVariableName"/> が "1" のときだけ実行し、それ以外は Skip する。
/// <c>dotnet test Othello.slnx</c> などの通常のテスト実行でウィンドウが起動しないようにするため。
/// </summary>
public sealed class UiFactAttribute : FactAttribute
{
	/// <summary>GUI テストを有効にする環境変数名。</summary>
	public const string EnableVariableName = "OTHELLO_UI_TESTS";

	/// <summary>環境変数を確認し、無効な場合は Skip 理由を設定する。</summary>
	public UiFactAttribute()
	{
		if (Environment.GetEnvironmentVariable(EnableVariableName) != "1")
			Skip = $"GUI テストはウィンドウを起動するため、環境変数 {EnableVariableName}=1 のときのみ実行します。";
	}
}
