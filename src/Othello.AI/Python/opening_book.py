"""
opening_book.py - Opening Book（定石集）

序盤の探索が浅く評価値が不安定になりがちな局面に対し、少数の検証済み定石を
辞書引きで参照する。定石データは標準記譜法（列 a-h、行 1-8）の手順として定義し、
モジュール読み込み時に board.py で実際に再生して各手の合法性を assert で検証してから
盤面+手番をキーとする辞書 (_BOOK) に登録する。

同一局面に対して複数の定石が異なる手を指定する場合は、先に登録された定石を優先し、
後発の定石はその局面への登録をスキップする（シミュレーション自体は定石自身の手で継続する）。
これにより手作業での枝分かれ管理が不要になる。

呼び出し元（ai.py）は Rust／純 Python どちらのバックエンドを使う場合でも
この lookup() を経由するため、alpha_beta.py 等のバックエンド実装には一切手を加えない。
"""

from board import EMPTY, BLACK, WHITE, BOARD_SIZE, get_valid_moves, make_move, opponent

# 採用する定石（機械検証済み。詳細は docs/improvement-opening-book.md 参照）
OPENING_LINES = [
    {"name": "対角定石", "moves": ["f5", "d6", "c5", "f4"]},
    {"name": "並び定石", "moves": ["f5", "f4", "e3"]},
]


def _notation_to_rc(notation):
    """標準記譜法（例: "f5"）を (row, col) の 0-indexed 座標に変換する。"""
    col = ord(notation[0]) - ord('a')
    row = int(notation[1]) - 1
    return row, col


_MAX_INDEX = BOARD_SIZE - 1


def _identity(r, c):
    """恒等変換。"""
    return r, c


def _rotate_180(r, c):
    """盤面を180度回転させる。"""
    return _MAX_INDEX - r, _MAX_INDEX - c


def _reflect_main_diagonal(r, c):
    """主対角線（左上-右下）で鏡映させる。"""
    return c, r


def _reflect_anti_diagonal(r, c):
    """反対角線（右上-左下）で鏡映させる。"""
    return _MAX_INDEX - c, _MAX_INDEX - r


# オセロの初期配置は黒2石・白2石が中央で対角配置されているため、
# 90度/270度回転・水平/垂直鏡映は黒白を入れ替えないと初期配置と一致しない。
# 本ゲームは常に黒が先手固定のため、それらは「白が初手を打つ」という
# 実際には起こらない局面を生成してしまい使えない。
# 黒の合法初手集合 {d3, c4, f5, e6} を保つ以下の4変換のみを使用する。
_SYMMETRY_TRANSFORMS = [_identity, _rotate_180, _reflect_main_diagonal, _reflect_anti_diagonal]


def _initial_board():
    """オセロの標準初期配置の盤面を返す。"""
    board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
    board[3][3] = WHITE
    board[3][4] = BLACK
    board[4][3] = BLACK
    board[4][4] = WHITE
    return board


def _board_key(board):
    """盤面をハッシュ可能なタプルに変換する（辞書キー用）。"""
    return tuple(tuple(row) for row in board)


def build_book(lines):
    """
    定石手順のリストから局面 -> 着手 の辞書を構築する。

    各定石ラインについて、黒の合法初手集合を保つ4つの対称変換
    （恒等・180度回転・主対角線鏡映・反対角線鏡映、_SYMMETRY_TRANSFORMS）
    それぞれで着手座標を変換してシミュレートし、変換後の局面も併せて登録する（Issue #62）。
    これにより初手が f5 以外（d3/c4/e6）の対称な局面でも定石がヒットするようになる。

    各手を打つ前に get_valid_moves で合法性を検証し、非合法手が含まれる場合は
    AssertionError を送出する（定石データの誤りを構築時に検知するため）。

    Args:
        lines (list[dict]): {"name": str, "moves": list[str]} のリスト

    Returns:
        dict[tuple[tuple[tuple[int, ...], ...], int], tuple[int, int]]:
            (盤面キー, 手番) -> (row, col) の辞書

    Raises:
        AssertionError: 定石データ（対称変換後を含む）に非合法手が含まれる場合
    """
    book = {}
    for line in lines:
        for transform in _SYMMETRY_TRANSFORMS:
            board = _initial_board()
            player = BLACK
            for notation in line["moves"]:
                r, c = transform(*_notation_to_rc(notation))
                valid_moves = get_valid_moves(board, player)
                # ユーザー入力ではなく開発者管理の静的定石データ(OPENING_LINES)の構築時検証のため nosec。
                assert (r, c) in valid_moves, (  # nosec B101
                    f"{line['name']} ({transform.__name__}): 非合法手 {notation} -> ({r}, {c}) "
                    f"player={player} valid_moves={valid_moves}"
                )
                key = (_board_key(board), player)
                # 先に登録された定石を優先する（後発の重複キーは登録をスキップ）
                book.setdefault(key, (r, c))
                board = make_move(board, r, c, player)
                player = opponent(player)
    return book


# モジュール読み込み時に定石データを検証済み辞書として構築する
_BOOK = build_book(OPENING_LINES)


def lookup(board, player):
    """
    現在の盤面・手番に対応する定石の着手を返す。

    Args:
        board (list[list[int]]): 現在の盤面
        player (int): 手番のプレイヤーの色

    Returns:
        tuple[int, int] | None: 定石の着手 (row, col)、定石にない局面では None
    """
    return _BOOK.get((_board_key(board), player))
