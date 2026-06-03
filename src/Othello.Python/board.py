"""
board.py - オセロ盤面操作ユーティリティ

盤面は 8×8 の int 配列（リストのリスト）で表現する。
各セルの値: 0=Empty, 1=Black, 2=White（C# の PlayerColor enum と対応）。

このモジュールの関数は盤面を直接変更せず、新しい配列を返す（副作用なし）。
"""

# 盤面の一辺のサイズ（オセロは常に 8×8）
BOARD_SIZE = 8

# セルの状態を表す定数（C# の PlayerColor enum と同じ int 値）
EMPTY, BLACK, WHITE = 0, 1, 2

# 探索する 8 方向のベクトル (dRow, dCol)
# 上・右上・右・右下・下・左下・左・左上 の順
DIRS = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def opponent(player):
    """
    指定したプレイヤーの相手色を返す。

    Args:
        player (int): BLACK または WHITE

    Returns:
        int: 相手の色（BLACK → WHITE, WHITE → BLACK）

    Raises:
        ValueError: player が BLACK / WHITE 以外の場合
    """
    if player == BLACK:
        return WHITE
    if player == WHITE:
        return BLACK
    raise ValueError(f"Invalid player value: {player}. Must be BLACK({BLACK}) or WHITE({WHITE}).")


def get_flips(board, r, c, player):
    """
    指定した座標 (r, c) に player の石を置いた際に反転される相手石の座標リストを返す。
    8 方向を走査し、挟める方向の相手石をすべて収集する。

    Args:
        board (list[list[int]]): 現在の盤面（変更しない）
        r (int): 石を置く行（0-7）
        c (int): 石を置く列（0-7）
        player (int): 石を置くプレイヤーの色

    Returns:
        list[tuple[int, int]]: 反転される相手石の (row, col) リスト
    """
    opp = opponent(player)
    flips = []

    for dr, dc in DIRS:
        line = []  # この方向で反転候補となる相手石を一時的に格納する
        nr, nc = r + dr, c + dc

        # 相手色の連続する石を末端に向かってスキャンする
        while 0 <= nr < BOARD_SIZE and 0 <= nc < BOARD_SIZE and board[nr][nc] == opp:
            line.append((nr, nc))
            nr += dr
            nc += dc

        # 連続した相手石の先に自分の石があれば反転確定
        if line and 0 <= nr < BOARD_SIZE and 0 <= nc < BOARD_SIZE and board[nr][nc] == player:
            flips.extend(line)
        # 盤端に到達、または空きマスで途切れた場合は反転なし

    return flips


def get_valid_moves(board, player):
    """
    指定したプレイヤーが着手できる全有効座標のリストを返す。
    1 枚以上の相手石を反転できる空きマスのみが有効手となる。

    Args:
        board (list[list[int]]): 現在の盤面
        player (int): 対象プレイヤーの色

    Returns:
        list[tuple[int, int]]: 有効な着手先 (row, col) のリスト
    """
    moves = []
    for r in range(BOARD_SIZE):
        for c in range(BOARD_SIZE):
            # 空きマスかつ 1 枚以上反転できる場合のみ有効手とする
            if board[r][c] == EMPTY and get_flips(board, r, c, player):
                moves.append((r, c))
    return moves


def make_move(board, r, c, player):
    """
    指定した座標に player の石を置き、反転処理を行った新しい盤面を返す。
    元の盤面は変更しない（イミュータブルな操作）。
    AI の探索木生成で多数のコピーが生成されるため、浅いコピーで十分（各行は int のリスト）。

    Args:
        board (list[list[int]]): 元の盤面（変更しない）
        r (int): 着手する行（0-7）
        c (int): 着手する列（0-7）
        player (int): 着手するプレイヤーの色

    Returns:
        list[list[int]]: 着手・反転後の新しい盤面
    """
    # 元の盤面を行ごとにシャローコピーして新しい盤面を生成する
    new_board = [row[:] for row in board]
    new_board[r][c] = player  # 指定マスに石を置く

    # 反転する相手石を自分の色に変える
    for fr, fc in get_flips(board, r, c, player):
        new_board[fr][fc] = player

    return new_board
