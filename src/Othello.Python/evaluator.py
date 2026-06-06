"""
evaluator.py - オセロ盤面の評価関数

中盤評価（evaluate）と終局評価（evaluate_final）の 2 つを提供する。
alpha_beta.py の探索木でリーフノードの評価値を算出するために使用する。

評価方針:
    - 位置重み（WEIGHTS）: コーナーや辺など戦略的に重要なマスに高いスコアを付与する
    - Mobility（着手可能数）: 着手の選択肢が多いほど有利とみなす
    - 終局評価: 石数差で最終的な勝敗を大きな値で表現する
"""

from board import count_valid_moves, opponent, BOARD_SIZE

# 盤面の位置重みテーブル（8×8）
# コーナー（0,0 等）: +100  取られると取り返せないため最優先
# X-square（コーナー斜め隣）: -50  相手にコーナーを与えるリスクが高い
# 辺（端の行・列）: +10  比較的安定した位置
# 中央付近: +1〜+5  ゲーム序盤は重要だが終盤では相対的に価値が下がる
WEIGHTS = [
    [100, -20, 10,  5,  5, 10, -20, 100],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [ 10,  -2,  5,  1,  1,  5,  -2,  10],
    [  5,  -2,  1,  2,  2,  1,  -2,   5],
    [  5,  -2,  1,  2,  2,  1,  -2,   5],
    [ 10,  -2,  5,  1,  1,  5,  -2,  10],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [100, -20, 10,  5,  5, 10, -20, 100],
]


def evaluate(board, player):
    """
    中盤の盤面を評価し、AI（player）にとっての評価値を返す。
    値が大きいほど player にとって有利な状態を示す。

    評価要素:
        1. 位置重み合計（自分の石 - 相手の石）
        2. Mobility（自分の有効手数 - 相手の有効手数）× 10

    Args:
        board (list[list[int]]): 評価対象の盤面
        player (int): AI が担当するプレイヤーの色

    Returns:
        int: 評価値（正 → player 有利、負 → 相手有利）
    """
    opp = opponent(player)
    score = 0

    # 1. 位置重みの合計差を計算する
    for r in range(BOARD_SIZE):
        for c in range(BOARD_SIZE):
            if board[r][c] == player:
                score += WEIGHTS[r][c]   # 自分の石は加算
            elif board[r][c] == opp:
                score -= WEIGHTS[r][c]   # 相手の石は減算

    # 2. Mobility: 自分の着手数が相手より多いほど有利（件数のみ数えて高速化）
    my_moves  = count_valid_moves(board, player)
    opp_moves = count_valid_moves(board, opp)
    score += (my_moves - opp_moves) * 10  # 係数 10 で位置重みと重みのバランスを取る

    return score


def evaluate_final(board, player, depth=0):
    """
    終局後の盤面を評価し、勝敗を大きな値で返す。
    探索木のリーフが終局（両者パスで終了）に達した場合に呼ばれる。

    残り探索深さ depth を加味することで、同じ勝敗でも「より早く決まる勝ち」を高く、
    「より早く決まる負け」を低く評価する（残り depth が大きい＝浅い手数で決着）。
    これにより AI は最短で勝ちにいき、負けは可能な限り引き延ばす。

    Args:
        board (list[list[int]]): 終局後の盤面
        player (int): AI が担当するプレイヤーの色
        depth (int): その終局に達した時点での残り探索深さ（既定 0）

    Returns:
        int: 勝利 → +(10000 + depth)、敗北 → -(10000 + depth)、引き分け → 0
    """
    opp = opponent(player)

    # 盤面全体の石数を数える
    my_count  = sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if board[r][c] == player)
    opp_count = sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if board[r][c] == opp)

    if my_count > opp_count:
        return 10000 + depth     # 石数が多い → 勝利（早い勝ちほど高評価）
    elif my_count < opp_count:
        return -(10000 + depth)  # 石数が少ない → 敗北（早い負けほど低評価）
    return 0                     # 同数 → 引き分け
