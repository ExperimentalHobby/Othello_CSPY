"""
test_opening_book.py - opening_book.py の単体テスト

定石辞書の参照 (lookup) と、定石データの合法性検証 (build_book) を検証する。
"""

import unittest

from board import BLACK, WHITE, make_move, get_valid_moves
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
        対称展開（Issue #62）により黒の合法初手4種（d3/c4/f5/e6）はすべて定石に含まれるようになったため、
        初期盤面では反転できない a2=(1,0) を用いて定石に存在しない局面を作る。
        パス条件: lookup が None を返すこと。"""
        board = _initial_board()
        board = make_move(board, 1, 0, BLACK)  # a2: 初期盤面では反転できず定石にも存在しない
        self.assertIsNone(opening_book.lookup(board, WHITE))

    def test_symmetric_first_move_d3_hits_transformed_response(self):
        """初期盤面から対称な初手 d3=(2,3)（f5 の反対角線鏡映）を打った局面で、
        対角定石を反対角線鏡映した応手 c5=(4,2) が返ることを確認する（Issue #62）。
        パス条件: lookup が (4, 2) を返すこと。"""
        board = _initial_board()
        board = make_move(board, 2, 3, BLACK)  # d3
        self.assertEqual(opening_book.lookup(board, WHITE), (4, 2))  # c5

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


class SymmetryTransformTests(unittest.TestCase):
    """座標変換関数（対称性展開に使用）の正当性を検証する。"""

    def test_rotate_180_transforms_f5_to_c4(self):
        """180度回転で f5=(4,5) が c4=(3,2) に変換されることを確認する。
        パス条件: _rotate_180(4, 5) が (3, 2) を返すこと。"""
        self.assertEqual(opening_book._rotate_180(4, 5), (3, 2))

    def test_reflect_main_diagonal_transforms_f5_to_e6(self):
        """主対角線鏡映で f5=(4,5) が e6=(5,4) に変換されることを確認する。
        パス条件: _reflect_main_diagonal(4, 5) が (5, 4) を返すこと。"""
        self.assertEqual(opening_book._reflect_main_diagonal(4, 5), (5, 4))

    def test_reflect_anti_diagonal_transforms_f5_to_d3(self):
        """反対角線鏡映で f5=(4,5) が d3=(2,3) に変換されることを確認する。
        パス条件: _reflect_anti_diagonal(4, 5) が (2, 3) を返すこと。"""
        self.assertEqual(opening_book._reflect_anti_diagonal(4, 5), (2, 3))

    def test_all_transforms_preserve_black_legal_first_move_set(self):
        """採用する4変換が、黒の合法初手集合 {d3, c4, f5, e6} をそれ自身へ写すことを確認する
        （90度/270度回転・水平/垂直鏡映は黒白の入れ替えが必要になり初手から非合法になるため、
        誤って _SYMMETRY_TRANSFORMS に追加してしまう回帰を防ぐガード）。
        パス条件: 各変換で f5 を変換した結果が、初期盤面での黒の合法初手集合に含まれること。"""
        board = _initial_board()
        black_legal_moves = set(get_valid_moves(board, BLACK))
        f5 = opening_book._notation_to_rc("f5")

        for transform in opening_book._SYMMETRY_TRANSFORMS:
            transformed = transform(*f5)
            self.assertIn(transformed, black_legal_moves,
                f"{transform.__name__} で f5={f5} を変換した {transformed} が黒の合法初手集合 "
                f"{black_legal_moves} に含まれること")


class BuildBookValidationTests(unittest.TestCase):
    """build_book() の合法性検証ロジックを確認する。"""

    def test_actual_opening_lines_build_without_error_for_all_symmetry_transforms(self):
        """実際の OPENING_LINES データが、採用する4つの対称変換すべてで
        AssertionError を送出せずに構築できることを確認する（Issue #62 テスト観点3）。
        モジュール読み込み時の _BOOK 構築（build_book(OPENING_LINES)）と同じ呼び出しを
        明示的なテストとして実行し、意図を明確にする。
        パス条件: build_book(OPENING_LINES) が例外を送出せず辞書を返すこと。"""
        book = opening_book.build_book(opening_book.OPENING_LINES)
        self.assertIsInstance(book, dict)
        self.assertGreater(len(book), 0)

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
