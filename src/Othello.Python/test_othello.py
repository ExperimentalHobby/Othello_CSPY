"""
test_othello.py - Python AI（board / evaluator / alpha_beta）の単体テスト

標準ライブラリ unittest のみを使用する（追加インストール不要）。

実行方法（リポジトリルートから）:
    py -m unittest discover -s src/Othello.Python -p "test_*.py"
"""

import unittest

from board import (
    EMPTY, BLACK, WHITE, BOARD_SIZE,
    opponent, get_flips, get_valid_moves, make_move,
)
from evaluator import evaluate, evaluate_final
from alpha_beta import AlphaBetaAI


def make_initial_board():
    """オセロの標準初期配置（中央 4 マス）の盤面を返す。"""
    board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
    board[3][3] = WHITE
    board[3][4] = BLACK
    board[4][3] = BLACK
    board[4][4] = WHITE
    return board


class OpponentTests(unittest.TestCase):
    """opponent() のテスト。"""

    def test_black_returns_white(self):
        self.assertEqual(opponent(BLACK), WHITE)

    def test_white_returns_black(self):
        self.assertEqual(opponent(WHITE), BLACK)

    def test_invalid_value_raises(self):
        # EMPTY（0）など BLACK/WHITE 以外は ValueError
        with self.assertRaises(ValueError):
            opponent(EMPTY)
        with self.assertRaises(ValueError):
            opponent(99)


class GetFlipsTests(unittest.TestCase):
    """get_flips() のテスト。"""

    def test_valid_move_flips_one_piece(self):
        # 初期盤面で黒が (2,3) に置くと白 (3,3) を挟んで反転できる
        board = make_initial_board()
        flips = get_flips(board, 2, 3, BLACK)
        self.assertIn((3, 3), flips)

    def test_no_flip_on_isolated_cell(self):
        # 隅 (0,0) は誰も挟めないため反転なし
        board = make_initial_board()
        self.assertEqual(get_flips(board, 0, 0, BLACK), [])


class GetValidMovesTests(unittest.TestCase):
    """get_valid_moves() のテスト。"""

    def test_initial_board_black_has_four_moves(self):
        board = make_initial_board()
        moves = get_valid_moves(board, BLACK)
        self.assertEqual(len(moves), 4)
        # オセロ初期盤面の黒の有効手は (2,3)(3,2)(4,5)(5,4)
        self.assertEqual(set(moves), {(2, 3), (3, 2), (4, 5), (5, 4)})


class MakeMoveTests(unittest.TestCase):
    """make_move() のテスト。"""

    def test_move_places_and_flips(self):
        board = make_initial_board()
        new_board = make_move(board, 2, 3, BLACK)
        # 置いたマスが黒になっている
        self.assertEqual(new_board[2][3], BLACK)
        # 挟まれた白 (3,3) が黒に反転している
        self.assertEqual(new_board[3][3], BLACK)

    def test_original_board_unchanged(self):
        # make_move は元の盤面を変更しない（イミュータブル）
        board = make_initial_board()
        _ = make_move(board, 2, 3, BLACK)
        self.assertEqual(board[2][3], EMPTY)
        self.assertEqual(board[3][3], WHITE)


class EvaluateTests(unittest.TestCase):
    """evaluate() / evaluate_final() のテスト。"""

    def test_corner_ownership_is_favorable(self):
        # コーナー (0,0) を黒が保有する盤面は黒にとって高評価になる
        board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        board[0][0] = BLACK
        self.assertGreater(evaluate(board, BLACK), 0)
        # 同じ盤面を白視点で見ると負（相手がコーナー保有）
        self.assertLess(evaluate(board, WHITE), 0)

    def test_final_win_lose_draw(self):
        # 黒が多い盤面
        win_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        self.assertEqual(evaluate_final(win_board, BLACK), 10000)
        self.assertEqual(evaluate_final(win_board, WHITE), -10000)

        # 黒白同数の盤面（上半分 黒・下半分 白）
        draw_board = [[BLACK] * BOARD_SIZE if r < 4 else [WHITE] * BOARD_SIZE
                      for r in range(BOARD_SIZE)]
        self.assertEqual(evaluate_final(draw_board, BLACK), 0)


class AlphaBetaTests(unittest.TestCase):
    """AlphaBetaAI.get_best_move() のテスト。"""

    def setUp(self):
        self.ai = AlphaBetaAI()

    def test_no_valid_moves_returns_none(self):
        # 全マス黒で埋まった盤面では白に有効手がない
        full_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        self.assertIsNone(self.ai.get_best_move(full_board, WHITE, depth=3))

    def test_returns_legal_move(self):
        # 初期盤面で返る手は黒の有効手のいずれか
        board = make_initial_board()
        move = self.ai.get_best_move(board, BLACK, depth=3)
        self.assertIn(move, get_valid_moves(board, BLACK))


if __name__ == "__main__":
    unittest.main()
