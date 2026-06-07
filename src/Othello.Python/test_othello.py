"""
test_othello.py - Python AI（board / evaluator / alpha_beta）の単体テスト

標準ライブラリ unittest のみを使用する（追加インストール不要）。

実行方法（リポジトリルートから）:
    py -m unittest discover -s src/Othello.Python -p "test_*.py"
"""

import unittest

from board import (
    EMPTY, BLACK, WHITE, BOARD_SIZE,
    opponent, get_flips, has_any_flip, get_valid_moves, count_valid_moves, make_move,
)
from evaluator import evaluate, evaluate_final, WEIGHTS
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
    """get_valid_moves() / has_any_flip() / count_valid_moves() のテスト。"""

    def test_initial_board_black_has_four_moves(self):
        """初期盤面で黒の有効手が 4 件かつ正しい座標であることを確認する。
        パス条件: 件数が 4 かつ {(2,3),(3,2),(4,5),(5,4)} と一致すること。"""
        board = make_initial_board()
        moves = get_valid_moves(board, BLACK)
        self.assertEqual(len(moves), 4)
        self.assertEqual(set(moves), {(2, 3), (3, 2), (4, 5), (5, 4)})

    def test_has_any_flip_matches_get_flips(self):
        """has_any_flip が get_flips の「反転可否」と全マスで一致することを確認する。
        パス条件: 初期盤面の全空きマス×両プレイヤーで bool(get_flips) == has_any_flip であること。"""
        board = make_initial_board()
        for player in (BLACK, WHITE):
            for r in range(BOARD_SIZE):
                for c in range(BOARD_SIZE):
                    if board[r][c] != EMPTY:
                        continue
                    self.assertEqual(
                        bool(get_flips(board, r, c, player)),
                        has_any_flip(board, r, c, player),
                        msg=f"({r},{c}) player={player}")

    def test_count_valid_moves_matches_get_valid_moves(self):
        """count_valid_moves が get_valid_moves の件数と一致することを確認する。
        パス条件: 初期盤面と一手後の盤面の両方で両プレイヤーの件数が一致すること。"""
        boards = [make_initial_board(), make_move(make_initial_board(), 2, 3, BLACK)]
        for board in boards:
            for player in (BLACK, WHITE):
                self.assertEqual(
                    count_valid_moves(board, player),
                    len(get_valid_moves(board, player)),
                    msg=f"player={player}")


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
        """終局評価が勝ち +10000・負け -10000・引き分け 0 を返すことを確認する（depth 既定 0）。
        パス条件: 全マス黒の盤面で黒が +10000、白が -10000、同数盤面で 0 であること。"""
        win_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        self.assertEqual(evaluate_final(win_board, BLACK), 10000)
        self.assertEqual(evaluate_final(win_board, WHITE), -10000)

        # 黒白同数の盤面（上半分 黒・下半分 白）
        draw_board = [[BLACK] * BOARD_SIZE if r < 4 else [WHITE] * BOARD_SIZE
                      for r in range(BOARD_SIZE)]
        self.assertEqual(evaluate_final(draw_board, BLACK), 0)

    def test_final_prefers_faster_decision_by_depth(self):
        """終局評価が残り depth を加味し、早い勝ちほど高く・早い負けほど低くなることを確認する。
        パス条件: 勝ちは depth が大きいほど大きい値、負けは depth が大きいほど小さい値、
        引き分けは depth に関わらず 0 であること。"""
        win_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        # 残り depth が大きい（＝浅い手数で決着）勝ちほど評価が高い
        self.assertGreater(evaluate_final(win_board, BLACK, depth=5),
                           evaluate_final(win_board, BLACK, depth=1))
        self.assertEqual(evaluate_final(win_board, BLACK, depth=5), 10005)
        # 早い負けほど評価が低い（より避けるべき）
        self.assertLess(evaluate_final(win_board, WHITE, depth=5),
                        evaluate_final(win_board, WHITE, depth=1))
        self.assertEqual(evaluate_final(win_board, WHITE, depth=5), -10005)

        # 引き分けは depth に依存しない
        draw_board = [[BLACK] * BOARD_SIZE if r < 4 else [WHITE] * BOARD_SIZE
                      for r in range(BOARD_SIZE)]
        self.assertEqual(evaluate_final(draw_board, BLACK, depth=7), 0)


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

    def test_handles_forced_opponent_pass(self):
        """探索中に相手が強制パスする局面でも、例外なく合法手を返すことを確認する。
        パス条件: 白が着手不能・黒のみ着手可能な盤面で、get_best_move が黒の合法手を返すこと。"""
        # (0,0)/(7,7) が空き、(0,1)/(7,6) が白、その他すべて黒。白は有効手なし。
        board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        board[0][0] = EMPTY
        board[0][1] = WHITE
        board[7][7] = EMPTY
        board[7][6] = WHITE

        move = self.ai.get_best_move(board, BLACK, depth=4)
        self.assertIn(move, [(0, 0), (7, 7)])


class AiMainLoopTests(unittest.TestCase):
    """ai.py のメインループ（stdin/stdout JSON IPC）の結合テスト。

    実際に ai.py をサブプロセスとして起動し、JSON リクエストを送受信して
    プロトコルが正しく機能することを確認する。
    """

    def _launch_ai(self):
        """ai.py をサブプロセスとして起動する。"""
        import subprocess
        import sys
        import os
        script = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'ai.py')
        return subprocess.Popen(
            [sys.executable, '-u', script],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding='utf-8',
            cwd=os.path.dirname(os.path.abspath(__file__)),
        )

    def _request(self, proc, board, player, depth=2):
        """JSON リクエストを 1 件送信してレスポンスを返す。"""
        import json
        proc.stdin.write(json.dumps({'board': board, 'player': player, 'depth': depth}) + '\n')
        proc.stdin.flush()
        return json.loads(proc.stdout.readline())

    def _close_ai(self, proc, timeout=5):
        """サブプロセスの全ハンドルを安全に閉じる。"""
        try:
            proc.stdin.close()
            proc.wait(timeout=timeout)
        finally:
            proc.stdout.close()
            proc.stderr.close()

    def test_returns_valid_move_for_initial_board(self):
        """初期盤面に対して有効な着手座標が返ることを確認する。
        パス条件: 'row'・'col' キーが存在し、返った座標が get_valid_moves に含まれること。"""
        board = make_initial_board()
        proc = self._launch_ai()
        try:
            res = self._request(proc, board, BLACK)
            self.assertIn('row', res)
            self.assertIn('col', res)
            self.assertNotIn('error', res)
            self.assertIn((res['row'], res['col']), get_valid_moves(board, BLACK))
        finally:
            self._close_ai(proc)

    def test_handles_sequential_requests(self):
        """同一プロセスで複数リクエストを順番に処理できることを確認する。
        パス条件: 3 往復分すべてのレスポンスが有効手であること。"""
        proc = self._launch_ai()
        try:
            board = make_initial_board()
            for _ in range(3):
                res = self._request(proc, board, BLACK)
                self.assertNotIn('error', res)
                move = (res['row'], res['col'])
                self.assertIn(move, get_valid_moves(board, BLACK))
                board = make_move(board, move[0], move[1], BLACK)

                if get_valid_moves(board, WHITE):
                    res = self._request(proc, board, WHITE)
                    self.assertNotIn('error', res)
                    move = (res['row'], res['col'])
                    self.assertIn(move, get_valid_moves(board, WHITE))
                    board = make_move(board, move[0], move[1], WHITE)
        finally:
            self._close_ai(proc, timeout=10)

    def test_returns_error_for_no_valid_moves(self):
        """有効手がない局面（全マス黒）に白として送ると error キーのレスポンスが返ることを確認する。
        パス条件: 'error' キーがレスポンスに含まれること。"""
        full_board = [[BLACK] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        proc = self._launch_ai()
        try:
            res = self._request(proc, full_board, WHITE)
            self.assertIn('error', res)
        finally:
            self._close_ai(proc)


class EvaluateComponentTests(unittest.TestCase):
    """evaluate() の個別評価要素（位置重み・Mobility）のテスト。"""

    def test_x_square_is_unfavorable(self):
        """X-square（コーナー斜め隣: (1,1) 等）を保有すると評価値が負になることを確認する。
        パス条件: board[1][1]=BLACK のみの盤面で evaluate(board, BLACK) < 0 であること
                  （WEIGHTS[1][1] = -50 なので黒視点で不利）。"""
        board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        board[1][1] = BLACK  # X-square: WEIGHTS[1][1] = -50
        self.assertLess(evaluate(board, BLACK), 0)

    def test_edge_position_is_favorable(self):
        """辺マス（コーナー以外の端: (0,2) 等）を保有すると評価値が正になることを確認する。
        パス条件: board[0][2]=BLACK のみの盤面で evaluate(board, BLACK) > 0 であること
                  （WEIGHTS[0][2] = 10 なので黒視点で有利）。"""
        board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        board[0][2] = BLACK  # Edge: WEIGHTS[0][2] = 10
        self.assertGreater(evaluate(board, BLACK), 0)

    def test_corner_is_better_than_x_square(self):
        """コーナーは X-square より evaluate 値が高いことを確認する。
        パス条件: コーナー (0,0) 保有時 > X-square (1,1) 保有時 の評価値であること。"""
        corner_board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        corner_board[0][0] = BLACK  # WEIGHTS[0][0] = 100

        xsq_board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
        xsq_board[1][1] = BLACK  # WEIGHTS[1][1] = -50

        self.assertGreater(evaluate(corner_board, BLACK), evaluate(xsq_board, BLACK))

    def test_evaluate_equals_positional_plus_mobility(self):
        """evaluate() の戻り値が「位置重み差 + Mobility 差 × 10」と等しいことを確認する。
        パス条件: 初期盤面を黒として評価した結果が公式と一致すること。"""
        board = make_initial_board()
        player = BLACK
        opp = opponent(player)

        positional = sum(
            WEIGHTS[r][c] if board[r][c] == player else
            (-WEIGHTS[r][c] if board[r][c] == opp else 0)
            for r in range(BOARD_SIZE)
            for c in range(BOARD_SIZE)
        )
        mobility = (count_valid_moves(board, player) - count_valid_moves(board, opp)) * 10

        self.assertEqual(evaluate(board, player), positional + mobility)


if __name__ == "__main__":
    unittest.main()
