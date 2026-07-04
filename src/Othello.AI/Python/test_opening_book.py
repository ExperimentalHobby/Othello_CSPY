"""
test_opening_book.py - opening_book.py の単体テスト

定石辞書の参照 (lookup) と、定石データの合法性検証 (build_book) を検証する。
"""

import unittest

from board import BLACK, WHITE, make_move
import opening_book


def _initial_board():
    from board import EMPTY, BOARD_SIZE
    board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
    board[3][3] = WHITE
    board[3][4] = BLACK
    board[4][3] = BLACK
    board[4][4] = WHITE
    return board


class OpeningBookLookupTests(unittest.TestCase):
    """OPENING_LINES から構築された定石辞書 (lookup) の振る舞いを検証する。"""

    def test_initial_position_returns_f5_for_black(self):
        """初期盤面・黒番では定石の初手 f5 = (4, 5) が返ることを確認する。
        パス条件: lookup が (4, 5) を返すこと。"""
        board = _initial_board()
        self.assertEqual(opening_book.lookup(board, BLACK), (4, 5))

    def test_non_book_position_returns_none(self):
        """定石にない局面では lookup が None を返すことを確認する。
        パス条件: 定石外の手 (2, 3)=d3 を打った局面で lookup が None を返すこと。"""
        board = _initial_board()
        board = make_move(board, 2, 3, BLACK)  # d3: 定石（f5始まり）には存在しない手
        self.assertIsNone(opening_book.lookup(board, WHITE))

    def test_diagonal_line_fourth_move_returns_f4_for_white(self):
        """対角定石 f5 d6 c5 まで進めた局面で、白番の4手目 f4=(3,5) が返ることを確認する。
        パス条件: lookup が (3, 5) を返すこと。"""
        board = _initial_board()
        board = make_move(board, 4, 5, BLACK)   # f5
        board = make_move(board, 5, 3, WHITE)   # d6
        board = make_move(board, 4, 2, BLACK)   # c5
        self.assertEqual(opening_book.lookup(board, WHITE), (3, 5))

    def test_namesake_line_branch_returns_e3_for_black(self):
        """並び定石の分岐 f5 f4 まで進めた局面（対角定石とは異なる白番の応手）で、
        黒番の3手目 e3=(2,4) が返ることを確認する。
        パス条件: lookup が (2, 4) を返すこと。"""
        board = _initial_board()
        board = make_move(board, 4, 5, BLACK)   # f5
        board = make_move(board, 3, 5, WHITE)   # f4 (対角定石の d6 とは異なる応手)
        self.assertEqual(opening_book.lookup(board, BLACK), (2, 4))


class BuildBookValidationTests(unittest.TestCase):
    """build_book() の合法性検証ロジックを確認する。"""

    def test_illegal_move_in_line_raises_assertion_error(self):
        """定石データに非合法手が含まれる場合、構築時に AssertionError が送出されることを確認する。
        パス条件: build_book が AssertionError を送出すること。"""
        bad_lines = [{"name": "不正な定石", "moves": ["d3", "c3", "d6"]}]  # d6 は非合法手
        with self.assertRaises(AssertionError):
            opening_book.build_book(bad_lines)

    def test_conflicting_line_does_not_overwrite_existing_entry(self):
        """同一局面に対して異なる手を指定する定石が後から来ても、
        先に登録された定石の手が優先されることを確認する。
        パス条件: 2本目の定石の2手目が登録されず、1本目の f5 応手が保持されること。"""
        lines = [
            {"name": "1本目", "moves": ["f5", "d6"]},
            {"name": "2本目", "moves": ["f5", "f4"]},
        ]
        book = opening_book.build_book(lines)
        board = _initial_board()
        board = make_move(board, 4, 5, BLACK)  # f5
        self.assertEqual(book[(opening_book._board_key(board), WHITE)], (5, 3))  # d6


if __name__ == "__main__":
    unittest.main()
