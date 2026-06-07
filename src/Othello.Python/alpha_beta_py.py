"""
alpha_beta_py.py - アルファベータ法によるオセロ AI（純 Python 実装 / フォールバック）

Rust 拡張モジュール othello_ai_rust が利用できない環境向けのフォールバック実装。
alpha_beta.py（シム）が Rust 拡張の import に失敗したとき、このモジュールの
AlphaBetaAI が使用される。挙動は Rust 実装と厳密に一致させてある。

アルゴリズム概要:
    1. 現在の盤面から有効手を列挙し、位置重みで事前ソート（ムーブオーダリング）する
    2. 各手を試して alpha-beta 探索を再帰的に行い、最善手を選択する
    3. パスが必要な場合はプレイヤーを入れ替えて同じ深さを続ける
    4. 終局（両者パス）または depth=0 で評価関数を呼び出す
"""

import time

from board import get_valid_moves, make_move, opponent
from evaluator import evaluate, evaluate_final, WEIGHTS


class _TimeoutError(Exception):
    """反復深化探索の時間制限超過を通知する内部例外。AlphaBetaAI 内でのみ使用する。"""
    pass


class AlphaBetaAI:
    """
    アルファベータ法でオセロの最善手を探索する AI クラス。
    インスタンスは状態を持たないため、複数ゲームで使い回せる。
    """

    def get_best_move(self, board, player, depth):
        """
        指定した盤面・プレイヤー・探索深さで最善手を返す。
        有効手がない場合は None を返す（呼び出し元でパス処理が必要）。

        Args:
            board (list[list[int]]): 現在の盤面
            player (int): AI が担当するプレイヤーの色
            depth (int): アルファベータ探索の最大深さ

        Returns:
            tuple[int, int] | None: 最善手の (row, col)、有効手なしの場合は None
        """
        moves = get_valid_moves(board, player)
        if not moves:
            return None  # 有効手なし → 呼び出し元でパス処理を行う

        # ムーブオーダリング: コーナーや辺など位置重みが高い手を先に探索することで
        # アルファベータの枝刈り効率を向上させる
        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)

        best_move  = moves[0]  # デフォルトとして最初の候補を設定する
        best_score = float('-inf')
        alpha      = float('-inf')
        beta       = float('inf')

        # 各候補手についてアルファベータ探索を実行し、最もスコアが高い手を選ぶ
        for move in moves:
            new_board = make_move(board, move[0], move[1], player)
            # player は最大化側なので次の階層は最小化（is_maximizing=False）
            score = self._alpha_beta(new_board, depth - 1, alpha, beta, False, player)
            if score > best_score:
                best_score = score
                best_move  = move
            # alpha を更新してこれより悪い手の探索をスキップする
            alpha = max(alpha, best_score)

        return best_move

    def get_best_move_timed(self, board, player, max_depth, time_ms):
        """
        反復深化探索（Iterative Deepening）で最善手を返す。
        深さ 1 から max_depth まで繰り返し探索し、time_ms を超えた時点で
        最後に完了した深さの結果を返す。

        Args:
            board (list[list[int]]): 現在の盤面
            player (int): AI が担当するプレイヤーの色
            max_depth (int): 反復深化の最大深さ
            time_ms (int): 時間制限（ミリ秒）

        Returns:
            tuple[int, int] | None: 最善手の (row, col)、有効手なしの場合は None
        """
        moves = get_valid_moves(board, player)
        if not moves:
            return None

        deadline  = time.monotonic() + time_ms / 1000.0
        best_move = moves[0]  # 最低限の初期値（深さ 1 の結果で必ず上書きされる）

        for depth in range(1, max_depth + 1):
            if time.monotonic() >= deadline:
                break
            try:
                result = self._get_best_move_at_depth(board, player, depth, deadline)
                if result is not None:
                    best_move = result
            except _TimeoutError:
                # 探索途中でタイムアウト → 最後に完了した深さの結果を返す
                break

        return best_move

    def _get_best_move_at_depth(self, board, player, depth, deadline):
        """指定した深さで最善手を探索する（時間切れ時は _TimeoutError を送出）。"""
        moves = get_valid_moves(board, player)
        if not moves:
            return None

        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)

        best_move  = moves[0]
        best_score = float('-inf')
        alpha      = float('-inf')
        beta       = float('inf')

        for move in moves:
            new_board = make_move(board, move[0], move[1], player)
            score = self._alpha_beta_timed(new_board, depth - 1, alpha, beta, False, player, deadline)
            if score > best_score:
                best_score = score
                best_move  = move
            alpha = max(alpha, best_score)

        return best_move

    def _alpha_beta_timed(self, board, depth, alpha, beta, is_maximizing, ai_player, deadline):
        """時間制限付きアルファベータ探索（時間切れ時は _TimeoutError を送出）。"""
        if time.monotonic() >= deadline:
            raise _TimeoutError

        if depth == 0:
            return evaluate(board, ai_player)

        current   = ai_player if is_maximizing else opponent(ai_player)
        opp       = opponent(current)
        moves     = get_valid_moves(board, current)
        opp_moves = get_valid_moves(board, opp)

        if not moves and not opp_moves:
            return evaluate_final(board, ai_player, depth)

        if not moves:
            return self._alpha_beta_timed(board, depth - 1, alpha, beta, not is_maximizing, ai_player, deadline)

        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)

        if is_maximizing:
            value = float('-inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = max(value, self._alpha_beta_timed(new_board, depth - 1, alpha, beta, False, ai_player, deadline))
                alpha = max(alpha, value)
                if alpha >= beta:
                    break
            return value
        else:
            value = float('inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = min(value, self._alpha_beta_timed(new_board, depth - 1, alpha, beta, True, ai_player, deadline))
                beta = min(beta, value)
                if alpha >= beta:
                    break
            return value

    def _alpha_beta(self, board, depth, alpha, beta, is_maximizing, ai_player):
        """
        アルファベータ探索の再帰関数。
        is_maximizing=True の場合は AI 側（最大化）、False の場合は相手側（最小化）として動作する。

        Args:
            board (list[list[int]]): 現在の盤面
            depth (int): 残りの探索深さ
            alpha (float): アルファ値（最大化側の下限）
            beta (float): ベータ値（最小化側の上限）
            is_maximizing (bool): True = 最大化プレイヤー（AI）のターン
            ai_player (int): AI が担当するプレイヤーの色（再帰全体で変わらない）

        Returns:
            float: この局面の評価値
        """
        # 深さ 0 に達した場合は評価関数で現在の盤面を評価する
        # get_valid_moves より前に判定することで、リーフノードでの無駄な手生成を回避する
        if depth == 0:
            return evaluate(board, ai_player)

        # 現在のターンのプレイヤーを決定する
        current = ai_player if is_maximizing else opponent(ai_player)
        opp     = opponent(current)  # current の相手色を一度だけ計算して再利用
        moves    = get_valid_moves(board, current)
        opp_moves = get_valid_moves(board, opp)

        # 両者ともに有効手がない場合は終局 → 終局評価を返す
        # 残り depth を渡して「早い決着」を選好させる
        if not moves and not opp_moves:
            return evaluate_final(board, ai_player, depth)

        if not moves:
            # 現在のプレイヤーに有効手がない → パス（相手にターンを渡す）
            # 深さは減らさず、is_maximizing を反転してパスを表現する
            return self._alpha_beta(board, depth - 1, alpha, beta, not is_maximizing, ai_player)

        # ムーブオーダリング: 再帰内でも位置重みで手をソートして枝刈り効率を向上させる
        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)

        if is_maximizing:
            # 最大化プレイヤー（AI）のターン: できるだけ高い評価値を選ぶ
            value = float('-inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = max(value, self._alpha_beta(new_board, depth - 1, alpha, beta, False, ai_player))
                alpha = max(alpha, value)
                if alpha >= beta:
                    # ベータカット: 最小化側がすでにこれより小さい値を持っているため探索不要
                    break
            return value
        else:
            # 最小化プレイヤー（相手）のターン: できるだけ低い評価値を選ぶ
            value = float('inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = min(value, self._alpha_beta(new_board, depth - 1, alpha, beta, True, ai_player))
                beta = min(beta, value)
                if alpha >= beta:
                    # アルファカット: 最大化側がすでにこれより大きい値を持っているため探索不要
                    break
            return value
