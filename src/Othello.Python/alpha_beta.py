"""
alpha_beta.py - AI バックエンド選択シム

オセロ AI の実装を 2 系統から選択する薄いシム:
    1. Rust 拡張モジュール othello_ai_rust（PyO3 製）が import できればそれを使う（高速）
    2. import に失敗した場合は純 Python 実装（alpha_beta_py.AlphaBetaAI）にフォールバックする

どちらも AlphaBetaAI クラスの同一インターフェース
（get_best_move(board, player, depth) -> tuple[int, int] | None）を提供するため、
呼び出し側（ai.py）は実装の違いを意識する必要がない。

C# ↔ Python の JSON プロトコルや ai.py のメインループは一切変更されない。
"""

try:
    # PyO3 製の Rust 拡張。ビルド済み(.pyd/.so)が import パス上にあれば成功する。
    import othello_ai_rust as _rust
except ImportError:
    _rust = None

# どちらのバックエンドで動作しているかを示す（テストや診断用）。
BACKEND = "rust" if _rust is not None else "python"


if _rust is not None:
    class AlphaBetaAI:
        """
        Rust 拡張 othello_ai_rust に処理を委譲する AI。
        着手選択の挙動は純 Python 版（alpha_beta_py.AlphaBetaAI）と厳密に一致する。
        ステートレスなので 1 インスタンスで全リクエストを処理できる。
        """

        def get_best_move(self, board, player, depth):
            """
            指定した盤面・プレイヤー・探索深さで最善手を返す。

            Args:
                board (list[list[int]]): 現在の盤面（8×8、0=Empty, 1=Black, 2=White）
                player (int): AI が担当するプレイヤーの色（1=Black, 2=White）
                depth (int): アルファベータ探索の最大深さ

            Returns:
                tuple[int, int] | None: 最善手の (row, col)、有効手なしの場合は None
            """
            return _rust.get_best_move(board, player, depth)
else:
    # Rust 拡張が無い環境向けの純 Python フォールバック
    from alpha_beta_py import AlphaBetaAI  # noqa: F401  （再エクスポート）
