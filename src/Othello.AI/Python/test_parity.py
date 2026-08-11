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

import alpha_beta_py
from board import (
    BLACK,
    BOARD_SIZE,
    EMPTY,
    WHITE,
    get_valid_moves,
    make_move,
    opponent,
)

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


@unittest.skipUnless(HAS_RUST, "Rust 拡張 othello_ai_rust が未ビルドのためスキップ")
class RustPythonInvalidInputParityTests(unittest.TestCase):
    """
    不正な入力（セル値域外・非8×8盤面）に対する Python/Rust 両バックエンドの
    例外送出を検証する（Issue #116）。

    Rust バックエンドの `pyo3_runtime.PanicException` は BaseException 直下であり
    Exception のサブクラスではないため、この一致検証はバックエンド分岐の回帰を検出する。
    セル値検証（out_of_range / negative）は Python 側も Rust 側と同様に明示的な
    ValueError を送出するよう実装済みのため型まで一致させて検証する。一方、盤面サイズ
    検証は Rust 側のみが明示チェック（ValueError）を持ち、Python 側は範囲外アクセスに
    よる IndexError に委ねているため、こちらは型を問わず Exception のサブクラスで
    あることのみを検証する（型完全一致は Issue #116 の完了条件でも求めていない）。
    """

    def test_out_of_range_cell_value_raises_value_error_both_backends(self):
        """
        パス条件: セル値に 3（0/1/2 以外）を含む盤面で、Python/Rust 双方が
                  ValueError を送出すること。
        """
        board = make_initial_board()
        board[0][0] = 3  # 不正なセル値

        with self.assertRaises(ValueError):
            alpha_beta_py.AlphaBetaAI().get_best_move(board, BLACK, 2)
        with self.assertRaises(ValueError):
            othello_ai_rust.get_best_move(board, BLACK, 2)

    def test_negative_cell_value_raises_value_error_both_backends(self):
        """
        パス条件: セル値に -1（負値）を含む盤面で、Python/Rust 双方が
                  ValueError を送出すること。
        """
        board = make_initial_board()
        board[0][0] = -1  # 不正なセル値

        with self.assertRaises(ValueError):
            alpha_beta_py.AlphaBetaAI().get_best_move(board, BLACK, 2)
        with self.assertRaises(ValueError):
            othello_ai_rust.get_best_move(board, BLACK, 2)

    def test_non_square_board_raises_exception_subclass_both_backends(self):
        """
        パス条件: 8×8 でない盤面（7 行のみ）で、Python/Rust 双方が
                  Exception のサブクラスを送出すること（型は問わない。上記クラス docstring 参照）。
        """
        board = [[EMPTY] * BOARD_SIZE for _ in range(BOARD_SIZE - 1)]  # 7 行しかない

        # Python は IndexError、Rust は ValueError と型が異なる
        # （Issue #116: 盤面サイズ検証は Rust 側のみ明示チェックを持つ設計判断のため）。
        with self.assertRaises(Exception):  # noqa: B017
            alpha_beta_py.AlphaBetaAI().get_best_move(board, BLACK, 2)
        with self.assertRaises(Exception):  # noqa: B017
            othello_ai_rust.get_best_move(board, BLACK, 2)


if __name__ == "__main__":
    unittest.main()
