"""
alpha_beta.py - アルファベータ法によるオセロ AI

アルファベータ探索（Alpha-Beta Pruning）と ムーブオーダリングを組み合わせた
オセロ AI を実装する。探索深さ（depth）は DifficultyLevel に応じて C# 側から渡される。

アルゴリズム概要:
    1. 現在の盤面から有効手を列挙し、位置重みで事前ソート（ムーブオーダリング）する
    2. 各手を試して alpha-beta 探索を再帰的に行い、最善手を選択する
    3. パスが必要な場合はプレイヤーを入れ替えて同じ深さを続ける
    4. 終局（両者パス）または depth=0 で評価関数を呼び出す
"""

from board import get_valid_moves, make_move, opponent
from evaluator import evaluate, evaluate_final, WEIGHTS


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
        if not moves and not opp_moves:
            return evaluate_final(board, ai_player)

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
