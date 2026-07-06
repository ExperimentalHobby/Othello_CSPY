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

import random
import time
from enum import IntEnum

from board import BOARD_SIZE, get_valid_moves, make_move, opponent
from evaluator import evaluate, evaluate_final, WEIGHTS


class _TimeoutError(Exception):
    """反復深化探索の時間制限超過を通知する内部例外。AlphaBetaAI 内でのみ使用する。"""
    pass


class _NodeType(IntEnum):
    """トランスポジションテーブル（TT）のエントリ種別。C# 版 AlphaBetaAI.NodeType と同じ意味論。"""
    EXACT = 0        # αβ窓内の正確値
    LOWER_BOUND = 1  # βカット（fail-high）で得た下界値
    UPPER_BOUND = 2  # αを改善できなかった（fail-low）上界値


# Zobrist ハッシュ用の乱数テーブル。固定シードでモジュール読み込み時に 1 回だけ生成する。
# board[r][c] の値（0=Empty, 1=Black, 2=White）がそのまま添字になる。
# 暗号用途ではなく、C#/Rust 版と同一ハッシュ列を得るための決定論的乱数のため nosec。
_zobrist_rng = random.Random(42)  # nosec B311
_ZOBRIST = [[[_zobrist_rng.getrandbits(64) for _ in range(3)] for _ in range(BOARD_SIZE)]
            for _ in range(BOARD_SIZE)]


def _zobrist_hash(board):
    """
    盤面のみをハッシュ化した整数を返す（手番はハッシュに含めない）。
    TT のキーは (このハッシュ, is_maximizing) のタプルとし、手番の区別はそちらで行う。

    Args:
        board (list[list[int]]): 現在の盤面

    Returns:
        int: 盤面のハッシュ値
    """
    h = 0
    for r in range(BOARD_SIZE):
        for c in range(BOARD_SIZE):
            h ^= _ZOBRIST[r][c][board[r][c]]
    return h


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
        # TT はこの呼び出し（1 回の探索）専用。呼び出しをまたいで永続化しない
        # （C# 版 GetBestMoveFixedDepth と同じ設計。メモリ上限や eviction が不要になる）。
        tt = {}

        # 各候補手についてアルファベータ探索を実行し、最もスコアが高い手を選ぶ
        for move in moves:
            new_board = make_move(board, move[0], move[1], player)
            # player は最大化側なので次の階層は最小化（is_maximizing=False）
            score = self._alpha_beta(new_board, depth - 1, alpha, beta, False, player, tt)
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

        # Rust 版（best_move_timed）と一致させるため、deadline 設定前にソートして初期値を決める。
        # ソート前の moves[0]（行優先の先頭）は重みが最大とは限らないため、
        # 時間切れで深さ 1 の探索が完了しない場合でもムーブオーダリング後の最良手が返るようにする。
        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)
        deadline  = time.monotonic() + time_ms / 1000.0
        best_move = moves[0]  # ソート後の先頭（最大重みの手）を初期フォールバックとする

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
        # 深さごとに TT を作り直す（C# 版 GetBestMoveIterativeDeepening と同じ。
        # 深さをまたいだ TT 再利用は行わない＝安全側）。
        tt = {}

        for move in moves:
            new_board = make_move(board, move[0], move[1], player)
            score = self._alpha_beta_timed(new_board, depth - 1, alpha, beta, False, player, deadline, tt)
            if score > best_score:
                best_score = score
                best_move  = move
            alpha = max(alpha, best_score)

        return best_move

    def _alpha_beta_timed(self, board, depth, alpha, beta, is_maximizing, ai_player, deadline, tt):
        """時間制限付きアルファベータ探索（時間切れ時は _TimeoutError を送出）。
        tt（トランスポジションテーブル）参照ロジックは _alpha_beta と同一。"""
        if time.monotonic() >= deadline:
            raise _TimeoutError

        h = _zobrist_hash(board)
        key = (h, is_maximizing)
        entry = tt.get(key)
        if entry is not None:
            cached_score, cached_depth, node_type = entry
            if cached_depth >= depth:
                if node_type == _NodeType.EXACT:
                    return cached_score
                elif node_type == _NodeType.LOWER_BOUND:
                    alpha = max(alpha, cached_score)
                elif node_type == _NodeType.UPPER_BOUND:
                    beta = min(beta, cached_score)
                if alpha >= beta:
                    return cached_score

        if depth == 0:
            value = evaluate(board, ai_player)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        current   = ai_player if is_maximizing else opponent(ai_player)
        opp       = opponent(current)
        moves     = get_valid_moves(board, current)
        opp_moves = get_valid_moves(board, opp)

        if not moves and not opp_moves:
            value = evaluate_final(board, ai_player, depth)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        if not moves:
            value = self._alpha_beta_timed(board, depth - 1, alpha, beta, not is_maximizing, ai_player, deadline, tt)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)
        original_alpha = alpha
        original_beta  = beta

        if is_maximizing:
            value = float('-inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = max(value, self._alpha_beta_timed(new_board, depth - 1, alpha, beta, False, ai_player, deadline, tt))
                alpha = max(alpha, value)
                if alpha >= beta:
                    break
        else:
            value = float('inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = min(value, self._alpha_beta_timed(new_board, depth - 1, alpha, beta, True, ai_player, deadline, tt))
                beta = min(beta, value)
                if alpha >= beta:
                    break

        # node_type の判定は退避しておいた original_alpha/original_beta と比較する
        # （ループ内で更新された alpha/beta と比較すると fail-high/fail-low の境界値を
        # 誤って Exact 扱いしてしまうバグになる）。
        node_type = (_NodeType.UPPER_BOUND if value <= original_alpha
                     else _NodeType.LOWER_BOUND if value >= original_beta
                     else _NodeType.EXACT)
        tt[key] = (value, depth, node_type)
        return value

    def _alpha_beta(self, board, depth, alpha, beta, is_maximizing, ai_player, tt):
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
            tt (dict): トランスポジションテーブル。呼び出し元の get_best_move が1回の探索用に
                新規作成したものをそのまま渡す（呼び出しをまたいで永続化しない）。
                キーは (盤面の Zobrist ハッシュ, is_maximizing)、値は (score, depth, _NodeType)。

        Returns:
            float: この局面の評価値
        """
        # TT 参照: 十分な深さで探索済みのエントリがあれば再探索を省略する
        # （C# 版 AlphaBetaAI.AlphaBeta と同じ順序・意味論）。
        h = _zobrist_hash(board)
        key = (h, is_maximizing)
        entry = tt.get(key)
        if entry is not None:
            cached_score, cached_depth, node_type = entry
            if cached_depth >= depth:
                if node_type == _NodeType.EXACT:
                    return cached_score
                elif node_type == _NodeType.LOWER_BOUND:
                    alpha = max(alpha, cached_score)
                elif node_type == _NodeType.UPPER_BOUND:
                    beta = min(beta, cached_score)
                if alpha >= beta:
                    return cached_score

        # 深さ 0 に達した場合は評価関数で現在の盤面を評価する
        # get_valid_moves より前に判定することで、リーフノードでの無駄な手生成を回避する
        if depth == 0:
            value = evaluate(board, ai_player)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        # 現在のターンのプレイヤーを決定する
        current = ai_player if is_maximizing else opponent(ai_player)
        opp     = opponent(current)  # current の相手色を一度だけ計算して再利用
        moves    = get_valid_moves(board, current)
        opp_moves = get_valid_moves(board, opp)

        # 両者ともに有効手がない場合は終局 → 終局評価を返す
        # 残り depth を渡して「早い決着」を選好させる
        if not moves and not opp_moves:
            value = evaluate_final(board, ai_player, depth)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        if not moves:
            # 現在のプレイヤーに有効手がない → パス（相手にターンを渡す）
            # パスも 1 手消費するため depth - 1 を渡す。is_maximizing を反転して相手ターンを表現する。
            # パスは分岐がなく窓の影響を受けないため、常に Exact として格納してよい。
            value = self._alpha_beta(board, depth - 1, alpha, beta, not is_maximizing, ai_player, tt)
            tt[key] = (value, depth, _NodeType.EXACT)
            return value

        # ムーブオーダリング: 再帰内でも位置重みで手をソートして枝刈り効率を向上させる
        moves.sort(key=lambda m: WEIGHTS[m[0]][m[1]], reverse=True)
        # node_type 判定用に窓の元の値を退避する（ループ内で alpha/beta が更新されるため）。
        original_alpha = alpha
        original_beta  = beta

        if is_maximizing:
            # 最大化プレイヤー（AI）のターン: できるだけ高い評価値を選ぶ
            value = float('-inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = max(value, self._alpha_beta(new_board, depth - 1, alpha, beta, False, ai_player, tt))
                alpha = max(alpha, value)
                if alpha >= beta:
                    # ベータカット: 最小化側がすでにこれより小さい値を持っているため探索不要
                    break
        else:
            # 最小化プレイヤー（相手）のターン: できるだけ低い評価値を選ぶ
            value = float('inf')
            for move in moves:
                new_board = make_move(board, move[0], move[1], current)
                value = min(value, self._alpha_beta(new_board, depth - 1, alpha, beta, True, ai_player, tt))
                beta = min(beta, value)
                if alpha >= beta:
                    # アルファカット: 最大化側がすでにこれより大きい値を持っているため探索不要
                    break

        # node_type の判定は退避しておいた original_alpha/original_beta と比較する
        # （ループ内で更新された alpha/beta と比較すると fail-high/fail-low の境界値を
        # 誤って Exact 扱いしてしまうバグになる。C# 版のコメントに残る F2 と同種のバグ）。
        node_type = (_NodeType.UPPER_BOUND if value <= original_alpha
                     else _NodeType.LOWER_BOUND if value >= original_beta
                     else _NodeType.EXACT)
        tt[key] = (value, depth, node_type)
        return value
