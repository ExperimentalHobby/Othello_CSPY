namespace Technopro.Othello.Core.Models;

/// <summary>
/// オセロ盤面上の座標を表す不変の値型（構造体）。
/// 行・列ともに 0 から 7 の範囲のみ有効で、範囲外はコンストラクタで例外を投げる。
/// </summary>
public struct Position : IEquatable<Position>
{
	/// <summary>行インデックス（0-7、上が 0）</summary>
	public int Row { get; }

	/// <summary>列インデックス（0-7、左が 0）</summary>
	public int Column { get; }

	/// <summary>
	/// 指定した行・列で Position を生成する。
	/// </summary>
	/// <param name="row">行インデックス（0-7）</param>
	/// <param name="column">列インデックス（0-7）</param>
	/// <exception cref="ArgumentException">row または column が 0-7 の範囲外の場合</exception>
	public Position(int row, int column)
	{
		// 範囲外の座標を早期に弾くことで不正状態の拡散を防ぐ
		if (!IsValid(row, column))
			throw new ArgumentException($"Invalid position: Row={row}, Column={column}. Must be 0-7.");
		Row = row;
		Column = column;
	}

	/// <summary>
	/// 指定した行・列がオセロ盤の有効範囲（0-7）内かどうかを判定する。
	/// Board の境界チェックや方向スキャン終端判定にも使用する。
	/// </summary>
	/// <param name="row">行インデックス</param>
	/// <param name="column">列インデックス</param>
	/// <returns>0 ≤ row ≤ 7 かつ 0 ≤ column ≤ 7 の場合 true</returns>
	public static bool IsValid(int row, int column) => row >= 0 && row < 8 && column >= 0 && column < 8;

	/// <summary>
	/// このインスタンスが有効な盤面座標かどうかを判定する。
	/// </summary>
	/// <returns>有効な範囲内であれば true</returns>
	public bool IsValid() => IsValid(Row, Column);

	/// <summary>
	/// 2 つの Position が同じ座標かどうかを比較する。
	/// </summary>
	/// <param name="obj">比較対象のオブジェクト</param>
	/// <returns>同じ行・列を持つ Position であれば true</returns>
	public override bool Equals(object? obj) => obj is Position p && Equals(p);

	/// <summary>
	/// 2 つの Position が同じ座標かどうかを比較する（型安全版）。
	/// </summary>
	/// <param name="other">比較対象の Position</param>
	/// <returns>Row と Column がともに等しければ true</returns>
	public bool Equals(Position other) => Row == other.Row && Column == other.Column;

	/// <summary>
	/// ハッシュコードを返す。Dictionary / HashSet のキーとして使用できる。
	/// </summary>
	/// <returns>Row と Column を組み合わせたハッシュ値</returns>
	public override int GetHashCode() => HashCode.Combine(Row, Column);

	/// <summary>
	/// デバッグ用文字列表現を返す。
	/// </summary>
	/// <returns>"(Row, Column)" 形式の文字列</returns>
	public override string ToString() => $"({Row}, {Column})";

	/// <summary>
	/// オセロの標準記譜法（列 a-h + 行 1-8）の文字列表現を返す。
	/// </summary>
	/// <returns>"a1"〜"h8" 形式の文字列</returns>
	public string ToNotation() => $"{(char)('a' + Column)}{Row + 1}";

	/// <summary>等値演算子。両辺の座標が同じ場合 true を返す。</summary>
	public static bool operator ==(Position left, Position right) => left.Equals(right);

	/// <summary>非等値演算子。両辺の座標が異なる場合 true を返す。</summary>
	public static bool operator !=(Position left, Position right) => !left.Equals(right);
}
