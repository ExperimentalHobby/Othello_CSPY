namespace Technopro.Othello.Core.Models;

/// <summary>
/// GameEngine.MakeMove の実行結果を表すクラス。
/// 成功・失敗のフラグ、メッセージ、反転した石のリストを保持する。
/// UI 側はこの結果をもとにアニメーションやエラー表示を行う。
/// </summary>
public class MoveResult
{
	/// <summary>手の配置が成功したかどうか</summary>
	public bool IsSuccess { get; }

	/// <summary>成功時の確認メッセージ、または失敗時のエラー理由</summary>
	public string Message { get; }

	/// <summary>
	/// この手によって反転された石の座標リスト。
	/// 成功時のみ有効。アニメーション処理などに利用できる。
	/// </summary>
	public List<Position> FlippedPieces { get; }

	/// <summary>
	/// MoveResult を直接生成する。ファクトリメソッド（Success / Failure）の利用を推奨する。
	/// </summary>
	/// <param name="isSuccess">成功フラグ</param>
	/// <param name="message">メッセージ文字列</param>
	/// <param name="flippedPieces">反転した石のリスト（null の場合は空リストに置換）</param>
	public MoveResult(bool isSuccess, string message, List<Position>? flippedPieces = null)
	{
		IsSuccess = isSuccess;
		Message = message;
		// null を渡された場合は空リストで初期化し、null チェックの手間を省く
		FlippedPieces = flippedPieces ?? new();
	}

	/// <summary>
	/// 成功を表す MoveResult を生成するファクトリメソッド。
	/// </summary>
	/// <param name="message">確認メッセージ（省略可）</param>
	/// <param name="flippedPieces">反転した石のリスト（省略可）</param>
	/// <returns>IsSuccess = true の MoveResult</returns>
	public static MoveResult Success(string message = "移動に成功しました", List<Position>? flippedPieces = null) =>
		new(true, message, flippedPieces);

	/// <summary>
	/// 失敗を表す MoveResult を生成するファクトリメソッド。
	/// </summary>
	/// <param name="message">失敗理由を説明するメッセージ</param>
	/// <returns>IsSuccess = false の MoveResult</returns>
	public static MoveResult Failure(string message) => new(false, message);

	/// <summary>
	/// デバッグ用文字列表現を返す。
	/// </summary>
	/// <returns>"[成功] メッセージ" または "[失敗] メッセージ" 形式の文字列</returns>
	public override string ToString() => $"[{(IsSuccess ? "成功" : "失敗")}] {Message}";
}
