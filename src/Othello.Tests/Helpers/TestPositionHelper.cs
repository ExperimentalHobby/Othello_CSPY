namespace Technopro.Othello.Tests.Helpers;

using Technopro.Othello.Core.Models;

/// <summary>テスト内で <see cref="Position"/> オブジェクトを生成・管理するヘルパー集。</summary>
public static class TestPositionHelper
{
    // 盤面四隅
    public static readonly Position TopLeft     = new(0, 0);
    public static readonly Position TopRight    = new(0, 7);
    public static readonly Position BottomLeft  = new(7, 0);
    public static readonly Position BottomRight = new(7, 7);

    // X-square（コーナー斜め隣 / 最もリスクが高い位置）
    public static readonly Position XSquareTopLeft     = new(1, 1);
    public static readonly Position XSquareTopRight    = new(1, 6);
    public static readonly Position XSquareBottomLeft  = new(6, 1);
    public static readonly Position XSquareBottomRight = new(6, 6);

    // 初期配置の中央 4 マス
    public static readonly Position Center1 = new(3, 3);  // White
    public static readonly Position Center2 = new(3, 4);  // Black
    public static readonly Position Center3 = new(4, 3);  // Black
    public static readonly Position Center4 = new(4, 4);  // White

    // 辺の中点
    public static readonly Position TopMid    = new(0, 3);
    public static readonly Position BottomMid = new(7, 3);
    public static readonly Position LeftMid   = new(3, 0);
    public static readonly Position RightMid  = new(3, 7);

    /// <summary>
    /// 行・列から <see cref="Position"/> を作成する。
    /// パス条件: 指定の行・列を持つ <see cref="Position"/> が返される。
    /// </summary>
    public static Position Create(int row, int col) => new(row, col);

    /// <summary>
    /// 座標が有効範囲（0〜7）内かどうかを返す。
    /// パス条件: 0〜7 の範囲なら <c>true</c>、範囲外なら <c>false</c>。
    /// </summary>
    public static bool IsValidPosition(int row, int col) => Position.IsValid(row, col);

    /// <summary>
    /// 初期盤面で黒が打てる 4 手を返す。
    /// パス条件: (2,3), (3,2), (4,5), (5,4) の 4 座標が返される。
    /// </summary>
    public static List<Position> GetOpeningMovesForBlack() =>
    [
        new(2, 3),
        new(3, 2),
        new(4, 5),
        new(5, 4),
    ];
}
