"""
test_parity.py - Rust 実装と純 Python 実装の整合性テスト

Rust 拡張(othello_ai_rust)と純 Python フォールバック(alpha_beta_py.AlphaBetaAI)が、
同一の盤面・プレイヤー・探索深さに対して「同じ着手」を返すことを検証する。
Rust への移植が挙動を変えていないこと（速くなるだけ）を担保するための回帰テスト。

Rust 拡張が未ビルドの環境では自動的にスキップされる。

実行方法（リポジトリルートから）:
    py -m unittest discover -s src/Othello.Python -p "test_*.py"
"""

import unittest

from board import (
    EMPTY, BLACK, WHITE, BOARD_SIZE,
    opponent, get_valid_moves, make_move,
)
import alpha_beta_py

try:
    import othello_ai_rust
    HAS_RUST = True
except ImportError:
    HAS_RUST = False


def make_initial_board():
    """オセロの標準初期配置の盤面を返す。"""
    board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE)]
    board[3][3] = WHITE
    board[3][4] = BLACK
    board[4][3] = BLACK
    board[4][4] = WHITE
    return board


@unittest.skipUnless(HAS_RUST, "Rust 拡張 othello_ai_rust が未ビルドのためスキップ")
class RustPythonParityTests(unittest.TestCase):
    """Rust 実装と純 Python 実装の着手選択が一致することを検証する。"""

    def _positions(self, count=12):
        """
        初期局面から純 Python AI で対局を進め、途中局面 (board, player) を列挙する。
        序盤〜中盤の多様な局面（パスを含む）を対象にするためのデータ生成。
        """
        py = alpha_beta_py.AlphaBetaAI()
        board = make_initial_board()
        player = BLACK
        positions = []

        for _ in range(count):
            # 着手前の局面（手番付き）を比較対象として記録する
            positions.append(([row[:] for row in board], player))

            if not get_valid_moves(board, player):
                # 手番側が打てない → パス。両者パスなら終局
                player = opponent(player)
                if not get_valid_moves(board, player):
                    break
                continue

            move = py.get_best_move(board, player, 3)
            board = make_move(board, move[0], move[1], player)
            player = opponent(player)

        return positions

    def test_same_move_across_positions_and_depths(self):
        """
        収集した各局面・各探索深さで Rust と純 Python が同じ手（または共に None）を返すことを確認する。
        パス条件: 全 (局面 × depth) で py の戻り値と rust の戻り値が一致すること。
        """
        py = alpha_beta_py.AlphaBetaAI()

        for board, player in self._positions():
            for depth in (1, 2, 3, 4, 5):
                py_move = py.get_best_move(board, player, depth)
                rust_move = othello_ai_rust.get_best_move(board, player, depth)
                # Rust は (row, col) タプルまたは None を返す。型を揃えて比較する。
                rust_move = tuple(rust_move) if rust_move is not None else None
                self.assertEqual(
                    py_move, rust_move,
                    msg=f"不一致: depth={depth} player={player} board={board}")

    def test_same_timed_move_across_positions(self):
        """
        時間制限付き反復深化（get_best_move_timed）で Rust と純 Python が同じ手を返すことを確認する。
        十分大きな time_ms（10 秒）を与えることで深さ 5 の探索が確実に完了し、
        結果が固定深さ探索（get_best_move depth=5）と一致することも副次的に確認できる。
        パス条件: 全局面で py_timed と rust_timed が一致すること。
        """
        py = alpha_beta_py.AlphaBetaAI()
        max_depth = 5
        time_ms   = 10_000  # 十分大きい値で「時間切れなし」を保証する

        for board, player in self._positions():
            py_timed   = py.get_best_move_timed(board, player, max_depth, time_ms)
            rust_timed = othello_ai_rust.get_best_move_timed(board, player, max_depth, time_ms)
            rust_timed = tuple(rust_timed) if rust_timed is not None else None
            self.assertEqual(
                py_timed, rust_timed,
                msg=f"timed 不一致: player={player} board={board}")


if __name__ == "__main__":
    unittest.main()
