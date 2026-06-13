namespace Technopro.Othello.Tests.Core.Models;

using Technopro.Othello.Core.Models;

public class BoardTests
{
    /// <summary>
    /// Board 生成直後にオセロの初期配置（中央 4 マス）が正しく設定されることを確認する。
    /// パス条件: 黒・白の石数がそれぞれ 2 であること。
    /// </summary>
    [Fact]
    public void Constructor_CreatesBoard_WithInitialPieces()
    {
        var board = new Board();
        Assert.Equal(2, board.CountPieces(PlayerColor.Black));
        Assert.Equal(2, board.CountPieces(PlayerColor.White));
    }

    /// <summary>
    /// 初期配置の座標 (3,3)=白・(3,4)=黒 を正しく取得できることを確認する。
    /// パス条件: GetPiece(3,3) が White、GetPiece(3,4) が Black を返すこと。
    /// </summary>
    [Fact]
    public void GetPiece_ReturnsCorrectColor()
    {
        var board = new Board();
        Assert.Equal(PlayerColor.White, board.GetPiece(3, 3));
        Assert.Equal(PlayerColor.Black, board.GetPiece(3, 4));
    }

    /// <summary>
    /// SetPiece で石を置いた後、同座標を GetPiece で取得すると置いた色が返ることを確認する。
    /// パス条件: GetPiece(0,0) が SetPiece で指定した Black を返すこと。
    /// </summary>
    [Fact]
    public void SetPiece_UpdatesPosition()
    {
        var board = new Board();
        board.SetPiece(0, 0, PlayerColor.Black);
        Assert.Equal(PlayerColor.Black, board.GetPiece(0, 0));
    }

    /// <summary>
    /// Clone() が元の盤面と独立したコピーを返すことを確認する（一方を変更しても他方に影響しない）。
    /// パス条件: クローン側の変更が元の盤面に反映されず、元が Empty のままであること。
    /// </summary>
    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var board1 = new Board();
        var board2 = board1.Clone();
        board2.SetPiece(0, 0, PlayerColor.White);
        Assert.Equal(PlayerColor.Empty, board1.GetPiece(0, 0));
        Assert.Equal(PlayerColor.White, board2.GetPiece(0, 0));
    }

    /// <summary>
    /// 初期盤面（中央 4 マス配置済み）の空きマスが 60 件であることを確認する。
    /// パス条件: GetEmptySquares の列挙件数が 60 であること。
    /// </summary>
    [Fact]
    public void GetEmptySquares_InitialBoard_Returns60Squares()
    {
        var board = new Board();
        var empty = board.GetEmptySquares().ToList();
        Assert.Equal(60, empty.Count);
    }

    /// <summary>
    /// 全マスを黒で埋めると空きマスが 0 件になることを確認する。
    /// パス条件: GetEmptySquares の列挙件数が 0 であること。
    /// </summary>
    [Fact]
    public void GetEmptySquares_FullBoard_ReturnsEmpty()
    {
        var board = new Board();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                board.SetPiece(r, c, PlayerColor.Black);

        Assert.Empty(board.GetEmptySquares());
    }

    /// <summary>
    /// 初期盤面の空きマス数が 60 であることを CountPieces(Empty) で確認する。
    /// パス条件: CountPieces(Empty) が 60 を返すこと。
    /// </summary>
    [Fact]
    public void CountPieces_Empty_OnInitialBoard_Returns60()
    {
        var board = new Board();
        Assert.Equal(60, board.CountPieces(PlayerColor.Empty));
    }

    /// <summary>
    /// 範囲外の行インデックスで GetPiece(int, int) を呼ぶと ArgumentException がスローされる。
    /// パス条件: ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void GetPiece_InvalidRow_ThrowsArgumentException()
    {
        var board = new Board();
        Assert.Throws<ArgumentException>(() => board.GetPiece(8, 0));
        Assert.Throws<ArgumentException>(() => board.GetPiece(-1, 0));
    }

    /// <summary>
    /// 範囲外の列インデックスで GetPiece(int, int) を呼ぶと ArgumentException がスローされる。
    /// パス条件: ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void GetPiece_InvalidColumn_ThrowsArgumentException()
    {
        var board = new Board();
        Assert.Throws<ArgumentException>(() => board.GetPiece(0, 8));
        Assert.Throws<ArgumentException>(() => board.GetPiece(0, -1));
    }

    /// <summary>
    /// 範囲外の座標で SetPiece(int, int, color) を呼ぶと ArgumentException がスローされる。
    /// パス条件: ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void SetPiece_InvalidPosition_ThrowsArgumentException()
    {
        var board = new Board();
        Assert.Throws<ArgumentException>(() => board.SetPiece(8, 0, PlayerColor.Black));
        Assert.Throws<ArgumentException>(() => board.SetPiece(0, -1, PlayerColor.Black));
    }

    /// <summary>
    /// Position オーバーロードの GetPiece が行・列オーバーロードと同じ結果を返す。
    /// パス条件: GetPiece(Position(3,3)) が White を返すこと。
    /// </summary>
    [Fact]
    public void GetPiece_ByPosition_ReturnsCorrectColor()
    {
        var board = new Board();
        Assert.Equal(PlayerColor.White, board.GetPiece(new Position(3, 3)));
        Assert.Equal(PlayerColor.Black, board.GetPiece(new Position(3, 4)));
    }

    /// <summary>
    /// Position オーバーロードの SetPiece で石を置いた後、同座標を GetPiece で取得すると置いた色が返る。
    /// パス条件: GetPiece(Position(0,0)) が Black を返すこと。
    /// </summary>
    [Fact]
    public void SetPiece_ByPosition_UpdatesPosition()
    {
        var board = new Board();
        board.SetPiece(new Position(0, 0), PlayerColor.Black);
        Assert.Equal(PlayerColor.Black, board.GetPiece(new Position(0, 0)));
    }

    /// <summary>
    /// ToString() が ●・○・・ の記号を含む非空文字列を返すことを確認する。
    /// パス条件: 戻り値が空でなく、● と ○ を含むこと。
    /// </summary>
    [Fact]
    public void ToString_InitialBoard_ContainsExpectedSymbols()
    {
        var board = new Board();
        var str = board.ToString();
        Assert.NotEmpty(str);
        Assert.Contains("●", str);
        Assert.Contains("○", str);
    }
}

public class PositionTests
{
    /// <summary>
    /// 有効範囲（0〜7）の行・列で Position を生成すると正しい座標が設定されることを確認する。
    /// パス条件: Row が 3、Column が 4 であること。
    /// </summary>
    [Fact]
    public void Constructor_ValidPosition_Succeeds()
    {
        var pos = new Position(3, 4);
        Assert.Equal(3, pos.Row);
        Assert.Equal(4, pos.Column);
    }

    /// <summary>
    /// 範囲外（8以上・負値・両方無効）の座標で Position を生成すると例外が投げられることを確認する。
    /// パス条件: 行超過・列超過・行負値・列負値・両方超過のすべてで ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void Constructor_InvalidPosition_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Position(8, 5));
        Assert.Throws<ArgumentException>(() => new Position(-1, 3));
        // 行が有効・列が無効
        Assert.Throws<ArgumentException>(() => new Position(0, 8));
        Assert.Throws<ArgumentException>(() => new Position(3, -1));
        // 両方無効
        Assert.Throws<ArgumentException>(() => new Position(8, 8));
    }

    /// <summary>
    /// 同じ行・列を持つ 2 つの Position が等値と判定されることを確認する。
    /// パス条件: Assert.Equal が成功すること（Equals / == が true を返すこと）。
    /// </summary>
    [Fact]
    public void Equals_SamePosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(3, 4);
        Assert.Equal(pos1, pos2);
    }

    /// <summary>
    /// 行・列が異なる 2 つの Position が非等値と判定されることを確認する。
    /// パス条件: Assert.NotEqual が成功すること（Equals / == が false を返すこと）。
    /// </summary>
    [Fact]
    public void Equals_DifferentPosition_ReturnsFalse()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(4, 3);
        Assert.NotEqual(pos1, pos2);
    }

    /// <summary>
    /// object 型のオーバーロードで同じ座標の Position を渡すと true を返す。
    /// パス条件: Equals(object) が true を返すこと。
    /// </summary>
    [Fact]
    public void Equals_BoxedSamePosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        object pos2 = new Position(3, 4);
        Assert.True(pos1.Equals(pos2));
    }

    /// <summary>
    /// object 型のオーバーロードで異なる型を渡すと false を返す。
    /// パス条件: Equals(object) が false を返すこと。
    /// </summary>
    [Fact]
    public void Equals_NonPositionObject_ReturnsFalse()
    {
        var pos = new Position(3, 4);
        Assert.False(pos.Equals("(3, 4)"));
        Assert.False(pos.Equals(null));
    }

    /// <summary>
    /// == 演算子で同じ座標の Position を比較すると true を返す。
    /// パス条件: == が true を返すこと。
    /// </summary>
    [Fact]
    public void EqualityOperator_SamePosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(3, 4);
        Assert.True(pos1 == pos2);
    }

    /// <summary>
    /// != 演算子で異なる座標の Position を比較すると true を返す。
    /// パス条件: != が true を返すこと。
    /// </summary>
    [Fact]
    public void InequalityOperator_DifferentPosition_ReturnsTrue()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(4, 3);
        Assert.True(pos1 != pos2);
    }

    /// <summary>
    /// 同じ座標を持つ Position は同じ GetHashCode を返す。
    /// パス条件: 2 つの GetHashCode が等しいこと。
    /// </summary>
    [Fact]
    public void GetHashCode_EqualPositions_ReturnSameHash()
    {
        var pos1 = new Position(3, 4);
        var pos2 = new Position(3, 4);
        Assert.Equal(pos1.GetHashCode(), pos2.GetHashCode());
    }

    /// <summary>
    /// ToString() が "(Row, Column)" 形式の文字列を返す。
    /// パス条件: ToString() が "(3, 4)" を返すこと。
    /// </summary>
    [Fact]
    public void ToString_ValidPosition_ReturnsExpectedFormat()
    {
        var pos = new Position(3, 4);
        Assert.Equal("(3, 4)", pos.ToString());
    }

    /// <summary>
    /// IsValid() インスタンスメソッドが構築済みの Position で true を返す。
    /// パス条件: IsValid() が true を返すこと（コンストラクタが検証済みのため常に true）。
    /// </summary>
    [Fact]
    public void IsValid_ConstructedPosition_ReturnsTrue()
    {
        var pos = new Position(0, 0);
        Assert.True(pos.IsValid());

        var corner = new Position(7, 7);
        Assert.True(corner.IsValid());
    }
}

public class PlayerColorTests
{
    /// <summary>
    /// Black.Opponent() が White を返すことを確認する。
    /// パス条件: 戻り値が PlayerColor.White であること。
    /// </summary>
    [Fact]
    public void Opponent_Black_ReturnsWhite()
    {
        Assert.Equal(PlayerColor.White, PlayerColor.Black.Opponent());
    }

    /// <summary>
    /// White.Opponent() が Black を返すことを確認する。
    /// パス条件: 戻り値が PlayerColor.Black であること。
    /// </summary>
    [Fact]
    public void Opponent_White_ReturnsBlack()
    {
        Assert.Equal(PlayerColor.Black, PlayerColor.White.Opponent());
    }

    /// <summary>
    /// Black.ToDisplayString() が日本語の "黒" を返すことを確認する。
    /// パス条件: 戻り値が "黒" であること。
    /// </summary>
    [Fact]
    public void ToDisplayString_Black_ReturnsJapaneseText()
    {
        Assert.Equal("黒", PlayerColor.Black.ToDisplayString());
    }

    /// <summary>
    /// White.ToDisplayString() が日本語の "白" を返すことを確認する。
    /// パス条件: 戻り値が "白" であること。
    /// </summary>
    [Fact]
    public void ToDisplayString_White_ReturnsJapaneseText()
    {
        Assert.Equal("白", PlayerColor.White.ToDisplayString());
    }

    /// <summary>
    /// Empty.ToDisplayString() が日本語の "空" を返すことを確認する。
    /// パス条件: 戻り値が "空" であること。
    /// </summary>
    [Fact]
    public void ToDisplayString_Empty_ReturnsJapaneseText()
    {
        Assert.Equal("空", PlayerColor.Empty.ToDisplayString());
    }

    /// <summary>
    /// 未定義の PlayerColor 値は ToDisplayString の default 分岐で "不明" を返す。
    /// パス条件: 戻り値が "不明" であること。
    /// </summary>
    [Fact]
    public void ToDisplayString_InvalidColor_ReturnsUnknown()
    {
        Assert.Equal("不明", ((PlayerColor)99).ToDisplayString());
    }

    /// <summary>
    /// Empty.Opponent() は相手が存在しないため ArgumentException をスローする。
    /// パス条件: ArgumentException がスローされること。
    /// </summary>
    [Fact]
    public void Opponent_Empty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PlayerColor.Empty.Opponent());
    }
}

public class GameStateTests
{
    /// <summary>
    /// GameState の全 7 状態について ToDisplayString() が期待の日本語文字列を返すことを確認する。
    /// パス条件: 各状態に対応する日本語文字列が返ること（GameState.cs の switch 式と一致）。
    /// </summary>
    [Fact]
    public void ToDisplayString_AllStates_ReturnExpectedJapaneseText()
    {
        Assert.Equal("初期化中",       GameState.Initialize.ToDisplayString());
        Assert.Equal("黒のターン",     GameState.BlackTurn.ToDisplayString());
        Assert.Equal("白のターン",     GameState.WhiteTurn.ToDisplayString());
        Assert.Equal("ゲーム終了",     GameState.GameOver.ToDisplayString());
        Assert.Equal("黒が勝ちました", GameState.BlackWon.ToDisplayString());
        Assert.Equal("白が勝ちました", GameState.WhiteWon.ToDisplayString());
        Assert.Equal("引き分け",       GameState.Draw.ToDisplayString());
    }
}

public class MoveResultTests
{
    /// <summary>
    /// MoveResult.Success に反転石リストを渡すと FlippedPieces にそのリストが格納されることを確認する。
    /// パス条件: FlippedPieces の件数と座標が渡したリストと一致すること。
    /// </summary>
    [Fact]
    public void Success_WithFlippedPieces_StoresFlippedPieces()
    {
        var flipped = new List<Position> { new(3, 3), new(4, 4) };
        var result = MoveResult.Success("成功", flipped);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.FlippedPieces.Count);
        Assert.Contains(new Position(3, 3), result.FlippedPieces);
        Assert.Contains(new Position(4, 4), result.FlippedPieces);
    }

    /// <summary>
    /// MoveResult.Failure は FlippedPieces が空リストであることを確認する。
    /// パス条件: IsSuccess が false かつ FlippedPieces が空であること。
    /// </summary>
    [Fact]
    public void Failure_HasEmptyFlippedPieces()
    {
        var result = MoveResult.Failure("無効な移動");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.FlippedPieces);
    }
}
