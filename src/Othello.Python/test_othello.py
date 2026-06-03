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
        """BLACK を渡すと WHITE が返ることを確認する。パス条件: 戻り値が WHITE であること。"""
        self.assertEqual(opponent(BLACK), WHITE)

    def test_white_returns_black(self):
        """WHITE を渡すと BLACK が返ることを確認する。パス条件: 戻り値が BLACK であること。"""
        self.assertEqual(opponent(WHITE), BLACK)

    def test_invalid_value_raises(self):
        """BLACK / WHITE 以外の値を渡すと ValueError が送出されることを確認する。
        パス条件: EMPTY（0）と 99 のいずれでも ValueError がスローされること。"""
        with self.assertRaises(ValueError):
            opponent(EMPTY)
        with self.assertRaises(ValueError):
            opponent(99)


class GetFlipsTests(unittest.TestCase):
    """get_flips() のテスト。"""

    def test_valid_move_flips_one_piece(self):
        """初期盤面で黒が (2,3) に置くと白 (3,3) が反転リストに含まれることを確認する。
        パス条件: flips に (3, 3) が含まれること。"""
        board = make_initial_board()
        flips = get_flips(board, 2, 3, BLACK)
        self.assertIn((3, 3), flips)

    def test_no_flip_on_isolated_cell(self):
        """石を挟めない隅 (0,0) では反転リストが空であることを確認する。
        パス条件: get_flips の戻り値が空リストであること。"""
        board = make_initial_board()
        self.assertEqual(get_flips(board, 0, 0, BLACK), [])


class GetValidMovesTests(unittest.TestCase):
    """get_valid_moves() のテスト。"""

    def test_initial_board_black_has_four_moves(self):
        """初期盤面で黒の有効手が 4 件かつ正しい座標であることを確認する。
        パス条件: 件数が 4 かつ {(2,3),(3,2),(4,5),(5,4)} と一致すること。"""
        board = make_initial_board()
        moves = get_valid_moves(board, BLACK)
        self.assertEqual(len(moves), 4)
        self.assertEqual(set(moves), {(2, 3), (3, 2), (4, 5), (5, 4)})


class MakeMoveTests(unittest.TestCase):
    """make_move() のテスト。"""

    def test_move_places_and_flips(self):
        """黒が (2,3) に着手すると指定マスが黒になり、挟まれた白 (3,3) が反転することを確認する。
        パス条件: new_board[2][3] が BLACK、new_board[3][3] が BLACK であること。"""
        board = make_initial_board()
        new_board = make_move(board, 2, 3, BLACK)
        self.assertEqual(new_board[2][3], BLACK)
        self.assertEqual(new_board[3][3], BLACK)

    def test_original_board_unchanged(self):
        """make_move が元の盤面を変更しない（イミュータブルな操作）ことを確認する。
        パス条件: 着手後も元の board[2][3] が EMPTY、board[3][3] が WHITE のままであること。"""
        board = make_initial_board()
        _ = make_move(board, 2, 3, BLACK)
        self.assertEqual(board[2][3], EMPTY)
        self.assertEqual(board[3][3], WHITE)


class EvaluateTests(unittest.TestCase):
    """evaluate() / evaluate_final() のテスト。"""

    def test_corner_ownership_is_favorable(self):
        """コーナーを保有する側の評価値が高くなることを確認する。
        パス条件: 黒がコーナー保有時に evaluate(board, BLACK) > 0、
        同じ盤面を白視点では evaluate(board, WHITE) < 0 であること。"""
        board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        board[0][0] = BLACK
        self.assertGreater(evaluate(board, BLACK), 0)
        self.assertLess(evaluate(board, WHITE), 0)

    def test_final_win_lose_draw(self):
        """終局評価が勝ち +10000・負け -10000・引き分け 0 を返すことを確認する。
        パス条件: 全マス黒の盤面で黒が +10000、白が -10000、同数盤面で 0 であること。"""
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
        """有効手がない場合に get_best_move が None を返すことを確認する。
        パス条件: 全マス黒の盤面で白として呼ぶと戻り値が None であること。"""
        full_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        self.assertIsNone(self.ai.get_best_move(full_board, WHITE, depth=3))

    def test_returns_legal_move(self):
        """get_best_move が有効手のいずれかを返すことを確認する。
        パス条件: 戻り値が get_valid_moves で列挙される有効手のリストに含まれること。"""
        board = make_initial_board()
        move = self.ai.get_best_move(board, BLACK, depth=3)
        self.assertIn(move, get_valid_moves(board, BLACK))


if __name__ == "__main__":
    unittest.main()
