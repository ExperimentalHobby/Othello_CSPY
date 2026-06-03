namespace Technopro.Othello.Core.Models;

/// <summary>
/// 8×8 のオセロ盤面状態を管理するクラス。
/// 内部に PlayerColor の 2 次元配列を保持し、石の配置・取得・クローンを提供する。
/// </summary>
public class Board
{
    /// <summary>盤面の内部表現（行×列の 2 次元配列）</summary>
    private readonly PlayerColor[,] _board;

    /// <summary>オセロ盤は常に 8×8</summary>
    private const int BoardSize = 8;

    /// <summary>
    /// 新しい Board インスタンスを生成し、オセロの初期配置（中央 4 マス）を設定する。
    /// </summary>
    public Board()
    {
        _board = new PlayerColor[BoardSize, BoardSize];
        InitializeBoard();
    }

    /// <summary>
    /// クローン用プライベートコンストラクタ。
    /// 元の Board の配列を Array.Clone で完全コピーするため、参照を共有しない。
    /// </summary>
    /// <param name="other">コピー元の Board</param>
    private Board(Board other)
    {
        // 2 次元配列を浅いコピーしても各要素は値型（enum）なので独立したコピーになる
        _board = (PlayerColor[,])other._board.Clone();
    }

    /// <summary>
    /// 盤面をオセロの初期配置にリセットする。
    /// 全マスを Empty にした後、中央 4 マスに黒・白を交互に配置する。
    /// </summary>
    private void InitializeBoard()
    {
        // 全マスを空に初期化
        for (int i = 0; i < BoardSize; i++)
            for (int j = 0; j < BoardSize; j++)
                _board[i, j] = PlayerColor.Empty;

        // オセロ標準の初期配置: 中央 4 マスに黒白を斜めに配置
        _board[3, 3] = PlayerColor.White;
        _board[3, 4] = PlayerColor.Black;
        _board[4, 3] = PlayerColor.Black;
        _board[4, 4] = PlayerColor.White;
    }

    /// <summary>
    /// 指定した Position の石の色を取得する。
    /// </summary>
    /// <param name="position">取得対象のマス（0-7, 0-7）</param>
    /// <returns>そのマスの PlayerColor（Empty / Black / White）</returns>
    public PlayerColor GetPiece(Position position) => GetPiece(position.Row, position.Column);

    /// <summary>
    /// 指定した行・列の石の色を取得する。
    /// </summary>
    /// <param name="row">行インデックス（0-7）</param>
    /// <param name="column">列インデックス（0-7）</param>
    /// <returns>そのマスの PlayerColor</returns>
    /// <exception cref="ArgumentException">row または column が 0-7 の範囲外の場合</exception>
    public PlayerColor GetPiece(int row, int column)
    {
        // 範囲外アクセスは ArgumentException で早期に弾く
        if (!Position.IsValid(row, column))
            throw new ArgumentException($"Invalid position: ({row}, {column})");
        return _board[row, column];
    }

    /// <summary>
    /// 指定した Position に石の色をセットする。
    /// </summary>
    /// <param name="position">配置対象のマス</param>
    /// <param name="color">配置する石の色</param>
    public void SetPiece(Position position, PlayerColor color) => SetPiece(position.Row, position.Column, color);

    /// <summary>
    /// 指定した行・列に石の色をセットする。
    /// </summary>
    /// <param name="row">行インデックス（0-7）</param>
    /// <param name="column">列インデックス（0-7）</param>
    /// <param name="color">配置する石の色</param>
    /// <exception cref="ArgumentException">row または column が 0-7 の範囲外の場合</exception>
    public void SetPiece(int row, int column, PlayerColor color)
    {
        if (!Position.IsValid(row, column))
            throw new ArgumentException($"Invalid position: ({row}, {column})");
        _board[row, column] = color;
    }

    /// <summary>
    /// この Board の完全な独立コピーを返す。
    /// Undo 用の履歴保存や AI の探索木生成など、盤面を変更せずに使いたい場面で利用する。
    /// </summary>
    /// <returns>内容が同一だが参照を共有しない新しい Board</returns>
    public Board Clone() => new(this);

    /// <summary>
    /// 指定した色の石が盤面に何個あるかを返す。
    /// </summary>
    /// <param name="color">カウント対象の色</param>
    /// <returns>その色の石の総数</returns>
    public int CountPieces(PlayerColor color)
    {
        int count = 0;
        for (int i = 0; i < BoardSize; i++)
            for (int j = 0; j < BoardSize; j++)
                if (_board[i, j] == color)
                    count++;
        return count;
    }

    /// <summary>
    /// 指定した色の石が置かれている全座標をイテレートして返す。
    /// </summary>
    /// <param name="color">対象の色</param>
    /// <returns>その色の石の Position の列挙</returns>
    public IEnumerable<Position> GetAllPieces(PlayerColor color)
    {
        for (int i = 0; i < BoardSize; i++)
            for (int j = 0; j < BoardSize; j++)
                if (_board[i, j] == color)
                    yield return new Position(i, j);
    }

    /// <summary>
    /// 石が置かれていない（Empty の）全マスの座標をイテレートして返す。
    /// 有効手の探索起点として OthelloRules から使われる。
    /// </summary>
    /// <returns>空きマスの Position の列挙</returns>
    public IEnumerable<Position> GetEmptySquares()
    {
        for (int i = 0; i < BoardSize; i++)
            for (int j = 0; j < BoardSize; j++)
                if (_board[i, j] == PlayerColor.Empty)
                    yield return new Position(i, j);
    }

    /// <summary>
    /// 盤面を初期配置に戻す。GameEngine.Initialize から呼ばれる。
    /// </summary>
    public void Reset() => InitializeBoard();

    /// <summary>
    /// デバッグ用に盤面を文字列で表現して返す。
    /// 黒=●、白=○、空=・ の記号を使用する。
    /// </summary>
    /// <returns>盤面の文字列表現</returns>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  0 1 2 3 4 5 6 7");
        for (int i = 0; i < BoardSize; i++)
        {
            sb.Append(i);
            for (int j = 0; j < BoardSize; j++)
            {
                sb.Append(' ');
                sb.Append(_board[i, j] switch
                {
                    PlayerColor.Black => "●",
                    PlayerColor.White => "○",
                    _ => "・"  // Empty
                });
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
