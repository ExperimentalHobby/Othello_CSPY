"""
evaluator.py - オセロ盤面の評価関数

中盤評価（evaluate）と終局評価（evaluate_final）の 2 つを提供する。
alpha_beta.py の探索木でリーフノードの評価値を算出するために使用する。

評価方針:
    - 位置重み（WEIGHTS）: コーナーや辺など戦略的に重要なマスに高いスコアを付与する
    - Stability（安定石）: ひっくり返せない石の差分を中盤評価に組み込む
    - Frontier（フロンティア）: 空きマスに隣接する石（不安定の代理指標）の差分を評価に組み込む
    - Mobility（着手可能数）: 着手の選択肢が多いほど有利とみなす
    - フェーズ切替: 空きマス数に応じて序盤/中盤/終盤で重みを動的に切り替える
    - 終局評価: 石数差で最終的な勝敗を大きな値で表現する
"""

from board import count_valid_moves, opponent, BOARD_SIZE

# 盤面の位置重みテーブル（8×8）
# コーナー（0,0 等）: +100  取られると取り返せないため最優先
# X-square（コーナー斜め隣）: -50  相手にコーナーを与えるリスクが高い
# 辺（端の行・列）: +10  比較的安定した位置
# 中央付近: +1〜+5  ゲーム序盤は重要だが終盤では相対的に価値が下がる
# 同一の値: Othello.Rust/src/lib.rs の WEIGHTS と一致させること（test_parity.py が担保）
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

# 安定石判定に用いる 4 軸（横・縦・斜め2方向）
_AXES = [(0, 1), (1, 0), (1, 1), (1, -1)]

# フロンティア判定に用いる 8 方向
_DIRS8 = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def _count_empty(board):
    """盤面上の空きマス数を返す。フェーズ判定に使用する。"""
    return sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if board[r][c] == 0)


def _is_half_axis_stable(board, stable, r, c, dr, dc):
    """(r, c) の (dr, dc) 半軸方向が安定しているかを返す。

    安定条件（いずれか）:
    1. 逆方向 (-dr,-dc) が即座に盤外 → 逆側からの挟み込みアンカーが存在できない
    2. (dr,dc) 方向が即座に盤外 → この方向は端
    3. (dr,dc) 方向の隣接マスが安定石
    4. (dr,dc) 方向の全ラインに空きなし → 配置不能
    """
    # 条件 1: 逆方向が盤外 → この半軸への攻撃のアンカーが存在できない
    opp_r, opp_c = r - dr, c - dc
    if not (0 <= opp_r < BOARD_SIZE and 0 <= opp_c < BOARD_SIZE):
        return True

    # 条件 2: この方向が即座に盤外
    nr, nc = r + dr, c + dc
    if not (0 <= nr < BOARD_SIZE and 0 <= nc < BOARD_SIZE):
        return True

    # 条件 3: 隣接マスが安定石
    if stable[nr][nc]:
        return True

    # 条件 4: この方向の全ラインに空きなし
    tr, tc = nr, nc
    while 0 <= tr < BOARD_SIZE and 0 <= tc < BOARD_SIZE:
        if board[tr][tc] == 0:
            return False
        tr += dr
        tc += dc
    return True


def _axis_stable(board, stable, r, c, dr, dc):
    """4 軸のうち 1 軸について安定判定を行う（両半軸ともに安定なら True）。"""
    return (_is_half_axis_stable(board, stable, r, c, dr, dc) and
            _is_half_axis_stable(board, stable, r, c, -dr, -dc))


def count_stable(board, player):
    """player の安定石（絶対にひっくり返せない石）の数を返す。

    コーナー起点の flood-fill: 4 軸すべてで安定している石を安定石とし、
    変化がなくなるまで繰り返し伝播させる。
    """
    stable = [[False] * BOARD_SIZE for _ in range(BOARD_SIZE)]
    changed = True
    while changed:
        changed = False
        for r in range(BOARD_SIZE):
            for c in range(BOARD_SIZE):
                if stable[r][c] or board[r][c] != player:
                    continue
                if all(_axis_stable(board, stable, r, c, dr, dc) for dr, dc in _AXES):
                    stable[r][c] = True
                    changed = True
    return sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if stable[r][c])


def count_frontier(board, player):
    """player の石のうち、空きマスに隣接している石（フロンティア）の数を返す。

    フロンティアは不安定性の代理指標。少ないほど守りやすい盤面とみなす。
    """
    count = 0
    for r in range(BOARD_SIZE):
        for c in range(BOARD_SIZE):
            if board[r][c] == player:
                if any(
                    0 <= r + dr < BOARD_SIZE and 0 <= c + dc < BOARD_SIZE
                    and board[r + dr][c + dc] == 0
                    for dr, dc in _DIRS8
                ):
                    count += 1
    return count


def evaluate(board, player):
    """
    中盤の盤面を評価し、AI（player）にとっての評価値を返す。
    値が大きいほど player にとって有利な状態を示す。

    空きマス数によってフェーズを切り替え、重みを動的に変化させる:
        序盤（empty > 44）: 位置重み + Mobility × 20
        中盤（20 ≤ empty ≤ 44）: 位置重み + Mobility × 10 + Stability × 25 + Frontier 差 × 5
        終盤（empty < 20）: 石数差 × 10 + 位置重み + Mobility × 10

    Args:
        board (list[list[int]]): 評価対象の盤面
        player (int): AI が担当するプレイヤーの色

    Returns:
        int: 評価値（正 → player 有利、負 → 相手有利）
    """
    opp = opponent(player)
    empty = _count_empty(board)

    # 位置重みスコア（全フェーズ共通）
    weight_score = 0
    for r in range(BOARD_SIZE):
        for c in range(BOARD_SIZE):
            if board[r][c] == player:
                weight_score += WEIGHTS[r][c]
            elif board[r][c] == opp:
                weight_score -= WEIGHTS[r][c]

    # Mobility スコア（全フェーズ共通）
    mobility = count_valid_moves(board, player) - count_valid_moves(board, opp)

    if empty > 44:
        # 序盤: Mobility を強調して選択肢の多さを重視する
        return weight_score + mobility * 20

    if empty < 20:
        # 終盤: 石数差を主成分とする
        my_count  = sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if board[r][c] == player)
        opp_count = sum(1 for r in range(BOARD_SIZE) for c in range(BOARD_SIZE) if board[r][c] == opp)
        return (my_count - opp_count) * 10 + weight_score + mobility * 10

    # 中盤: Stability + Frontier + Mobility を組み合わせる
    stability_score = (count_stable(board, player) - count_stable(board, opp)) * 25
    # フロンティアは少ないほど良い（相手のフロンティアが多いほど有利）
    frontier_score = (count_frontier(board, opp) - count_frontier(board, player)) * 5
    return weight_score + mobility * 10 + stability_score + frontier_score


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
