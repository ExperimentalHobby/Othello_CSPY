//! othello_ai_rust - オセロのアルファベータ探索 AI（Rust 実装）
//!
//! 既存の Python 実装（board.py / evaluator.py / alpha_beta.py）を Rust に移植したもの。
//! 着手選択の挙動は Python 版と厳密に一致させており（ムーブオーダリングの安定ソート、
//! `>` による先勝ち、パス処理、終局評価の depth 加点）、結果は変えずに高速化することが目的。
//!
//! PyO3 経由で `get_best_move(board, player, depth)` を Python へ公開する。
//! Python 側の薄いシム（alpha_beta.py）がこの関数に処理を委譲する。
//!
//! 盤面表現:
//!   - Python 境界: list[list[int]]（8×8、0=Empty, 1=Black, 2=White）
//!   - 内部:        フラットな [i8; 64]（index = row * 8 + col）で高速に扱う

// python フィーチャ無効時（cargo test など）は PyO3 公開関数が外れて純ロジックが
// 未使用扱いになるため、その構成に限り dead_code 警告を抑制する（テストでは使用される）。
#![cfg_attr(not(feature = "python"), allow(dead_code))]

#[cfg(feature = "python")]
use pyo3::exceptions::PyValueError;
#[cfg(feature = "python")]
use pyo3::prelude::*;
use std::collections::HashMap;
use std::sync::OnceLock;
use std::time::{Duration, Instant};

/// 盤面の一辺のサイズ（オセロは常に 8×8）
const SIZE: usize = 8;
/// 空きマスを表す値（Python / C# の PlayerColor.Empty と一致）
const EMPTY: i8 = 0;

/// 探索する 8 方向のベクトル (dRow, dCol)。board.py の DIRS と同順。
const DIRS: [(i32, i32); 8] = [
    (-1, -1),
    (-1, 0),
    (-1, 1),
    (0, -1),
    (0, 1),
    (1, -1),
    (1, 0),
    (1, 1),
];

/// 盤面の位置重みテーブル（evaluator.py の WEIGHTS と同一）。
const WEIGHTS: [[i32; SIZE]; SIZE] = [
    [100, -20, 10, 5, 5, 10, -20, 100],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [10, -2, 5, 1, 1, 5, -2, 10],
    [5, -2, 1, 2, 2, 1, -2, 5],
    [5, -2, 1, 2, 2, 1, -2, 5],
    [10, -2, 5, 1, 1, 5, -2, 10],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [100, -20, 10, 5, 5, 10, -20, 100],
];

/// アルファベータ探索の境界に用いる擬似無限大（Python の float('inf') 相当）。
const INF: i32 = i32::MAX;
const NEG_INF: i32 = i32::MIN;

/// トランスポジションテーブル（TT）のエントリ種別。C# 版 AlphaBetaAI.NodeType と同じ意味論。
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum NodeType {
    /// αβ窓内の正確値
    Exact,
    /// βカット（fail-high）で得た下界値
    LowerBound,
    /// αを改善できなかった（fail-low）上界値
    UpperBound,
}

/// TT のキー: (盤面の Zobrist ハッシュ, is_maximizing)。手番の区別はタプルの第2要素で行う
/// （C# 版は hash 単独キー + IsMaximizing フィールド照合だが、タプルキーの方が
/// 衝突時の上書き合戦が起きず単純に正しい）。
type TTKey = (u64, bool);

/// TT 本体。1 回の探索呼び出し（best_move 1 回、または best_move_timed の深さ 1 段）ごとに
/// 新規作成し、呼び出しをまたいで永続化しない（C# 版と同じ設計。eviction が不要になる）。
type TT = HashMap<TTKey, (i32, i32, NodeType)>;

/// splitmix64: 決定的な擬似乱数生成器。Zobrist テーブルの生成にのみ使う小さな実装。
/// 外部クレート（rand 等）を追加せずに済ませるため自前実装する。
fn splitmix64(seed: &mut u64) -> u64 {
    *seed = seed.wrapping_add(0x9E3779B97F4A7C15);
    let mut z = *seed;
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58476D1CE4E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D049BB133111EB);
    z ^ (z >> 31)
}

/// Zobrist ハッシュ用の乱数テーブルを固定シードで生成する（`board[r][c]` の値
/// 0=Empty/1=Black/2=White がそのまま添字になる）。
fn build_zobrist_table() -> [[[u64; 3]; SIZE]; SIZE] {
    let mut seed: u64 = 42;
    let mut table = [[[0u64; 3]; SIZE]; SIZE];
    for row in table.iter_mut() {
        for cell in row.iter_mut() {
            for slot in cell.iter_mut() {
                *slot = splitmix64(&mut seed);
            }
        }
    }
    table
}

static ZOBRIST: OnceLock<[[[u64; 3]; SIZE]; SIZE]> = OnceLock::new();

/// 盤面のみをハッシュ化した値を返す（手番はハッシュに含めない。TT キーの第2要素で区別する）。
fn zobrist_hash(board: &Board) -> u64 {
    let table = ZOBRIST.get_or_init(build_zobrist_table);
    let mut h: u64 = 0;
    for r in 0..SIZE {
        for c in 0..SIZE {
            h ^= table[r][c][board[idx(r, c)] as usize];
        }
    }
    h
}

/// 内部盤面型。フラット配列で row*8+col のインデックス参照を行う。
type Board = [i8; SIZE * SIZE];

/// (row, col) を内部配列インデックスへ変換する。
#[inline]
fn idx(r: usize, c: usize) -> usize {
    r * SIZE + c
}

/// 指定したプレイヤーの相手色を返す（BLACK(1) ⇔ WHITE(2)）。
/// 内部呼び出しは常に有効値を渡す前提（公開関数側で 1/2 を検証する）。
#[inline]
fn opponent(player: i8) -> i8 {
    3 - player
}

/// 座標 (r, c) に player を置いたとき反転する相手石のインデックスを out に集める。
/// 8 方向それぞれで相手石の連続を走査し、その先に自分の石がある方向のみ反転確定とする。
fn collect_flips(board: &Board, r: i32, c: i32, player: i8, out: &mut Vec<usize>) {
    let opp = opponent(player);

    for &(dr, dc) in DIRS.iter() {
        let mut line: Vec<usize> = Vec::new();
        let mut nr = r + dr;
        let mut nc = c + dc;

        // 相手色の連続を末端へ向かって走査する
        while nr >= 0
            && nr < SIZE as i32
            && nc >= 0
            && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == opp
        {
            line.push(idx(nr as usize, nc as usize));
            nr += dr;
            nc += dc;
        }

        // 連続した相手石の先に自分の石があれば反転確定
        if !line.is_empty()
            && nr >= 0
            && nr < SIZE as i32
            && nc >= 0
            && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == player
        {
            out.extend_from_slice(&line);
        }
    }
}

/// 座標 (r, c) に player が着手したとき 1 方向でも相手石を挟めるかを返す（短絡評価）。
/// 反転リストを構築しないため、有効手の有無判定・mobility 計算で高速。
fn has_any_flip(board: &Board, r: i32, c: i32, player: i8) -> bool {
    let opp = opponent(player);

    for &(dr, dc) in DIRS.iter() {
        let mut nr = r + dr;
        let mut nc = c + dc;
        let mut seen_opp = false;

        while nr >= 0
            && nr < SIZE as i32
            && nc >= 0
            && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == opp
        {
            seen_opp = true;
            nr += dr;
            nc += dc;
        }

        if seen_opp
            && nr >= 0
            && nr < SIZE as i32
            && nc >= 0
            && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == player
        {
            return true;
        }
    }

    false
}

/// player が着手できる全有効座標を行優先（row 外側・col 内側）で返す。
/// Python の get_valid_moves と列挙順を一致させることで、後段の安定ソート結果も一致する。
fn get_valid_moves(board: &Board, player: i8) -> Vec<(usize, usize)> {
    let mut moves = Vec::new();
    for r in 0..SIZE {
        for c in 0..SIZE {
            if board[idx(r, c)] == EMPTY && has_any_flip(board, r as i32, c as i32, player) {
                moves.push((r, c));
            }
        }
    }
    moves
}

/// player に 1 つでも有効手があるかを返す（座標リストを構築しない高速版）。
fn has_any_valid_move(board: &Board, player: i8) -> bool {
    for r in 0..SIZE {
        for c in 0..SIZE {
            if board[idx(r, c)] == EMPTY && has_any_flip(board, r as i32, c as i32, player) {
                return true;
            }
        }
    }
    false
}

/// player の有効手数を数えて返す（mobility 評価用）。
fn count_valid_moves(board: &Board, player: i8) -> i32 {
    let mut count = 0;
    for r in 0..SIZE {
        for c in 0..SIZE {
            if board[idx(r, c)] == EMPTY && has_any_flip(board, r as i32, c as i32, player) {
                count += 1;
            }
        }
    }
    count
}

/// 座標 (r, c) に player の石を置き、反転処理を行った新しい盤面を返す（元盤面は不変）。
fn make_move(board: &Board, r: usize, c: usize, player: i8) -> Board {
    let mut new_board = *board; // [i8; 64] は Copy なので値コピーで複製される
    new_board[idx(r, c)] = player;

    let mut flips = Vec::new();
    collect_flips(board, r as i32, c as i32, player, &mut flips);
    for f in flips {
        new_board[f] = player;
    }

    new_board
}

/// 安定石判定の半軸チェック（evaluator.py の _is_half_axis_stable と同一）。
///
/// 条件 1: 逆方向 (-dr,-dc) が盤外 → 攻撃アンカーが存在できない
/// 条件 2: (dr,dc) 方向が即座に盤外 → 端
/// 条件 3: (dr,dc) 方向の隣が安定石
/// 条件 4: (dr,dc) 方向のラインに空きなし
fn is_half_axis_stable(
    board: &Board,
    stable: &[bool; SIZE * SIZE],
    r: i32,
    c: i32,
    dr: i32,
    dc: i32,
) -> bool {
    // 条件 1: 逆方向が盤外
    let opp_r = r - dr;
    let opp_c = c - dc;
    if opp_r < 0 || opp_r >= SIZE as i32 || opp_c < 0 || opp_c >= SIZE as i32 {
        return true;
    }
    // 条件 2: この方向が即座に盤外
    let nr = r + dr;
    let nc = c + dc;
    if nr < 0 || nr >= SIZE as i32 || nc < 0 || nc >= SIZE as i32 {
        return true;
    }
    // 条件 3: 隣が安定石
    if stable[idx(nr as usize, nc as usize)] {
        return true;
    }
    // 条件 4: この方向の全ラインに空きなし
    let mut tr = nr;
    let mut tc = nc;
    while tr >= 0 && tr < SIZE as i32 && tc >= 0 && tc < SIZE as i32 {
        if board[idx(tr as usize, tc as usize)] == EMPTY {
            return false;
        }
        tr += dr;
        tc += dc;
    }
    true
}

/// 指定軸で安定しているかを返す（両半軸ともに安定なら true）。
fn is_axis_stable(
    board: &Board,
    stable: &[bool; SIZE * SIZE],
    r: i32,
    c: i32,
    dr: i32,
    dc: i32,
) -> bool {
    is_half_axis_stable(board, stable, r, c, dr, dc)
        && is_half_axis_stable(board, stable, r, c, -dr, -dc)
}

/// player の安定石数を返す（evaluator.count_stable と同一）。
/// 4 軸すべてで安定している石を安定石とし、変化がなくなるまで flood-fill する。
fn count_stable(board: &Board, player: i8) -> i32 {
    let mut stable = [false; SIZE * SIZE];
    let axes: [(i32, i32); 4] = [(0, 1), (1, 0), (1, 1), (1, -1)];

    let mut changed = true;
    while changed {
        changed = false;
        for r in 0..SIZE {
            for c in 0..SIZE {
                let i = idx(r, c);
                if stable[i] || board[i] != player {
                    continue;
                }
                if axes
                    .iter()
                    .all(|&(dr, dc)| is_axis_stable(board, &stable, r as i32, c as i32, dr, dc))
                {
                    stable[i] = true;
                    changed = true;
                }
            }
        }
    }

    stable.iter().filter(|&&s| s).count() as i32
}

/// player のフロンティア石数（空きマスに隣接する石）を返す（evaluator.count_frontier と同一）。
fn count_frontier(board: &Board, player: i8) -> i32 {
    let mut count = 0i32;
    for r in 0..SIZE {
        for c in 0..SIZE {
            if board[idx(r, c)] != player {
                continue;
            }
            if DIRS.iter().any(|&(dr, dc)| {
                let nr = r as i32 + dr;
                let nc = c as i32 + dc;
                nr >= 0
                    && nr < SIZE as i32
                    && nc >= 0
                    && nc < SIZE as i32
                    && board[idx(nr as usize, nc as usize)] == EMPTY
            }) {
                count += 1;
            }
        }
    }
    count
}

/// 中盤評価: フェーズ切替付き評価関数（evaluator.evaluate と同一）。
///
/// 空きマス数によってフェーズを切り替える:
///   序盤（empty > 44）: 位置重み + Mobility × 20
///   中盤（20 ≤ empty ≤ 44）: 位置重み + Mobility × 10 + Stability × 25 + Frontier 差 × 5
///   終盤（empty < 20）: 石数差 × 10 + 位置重み + Mobility × 10
fn evaluate(board: &Board, player: i8) -> i32 {
    let opp = opponent(player);

    // 位置重みスコア（全フェーズ共通）
    let mut weight_score = 0i32;
    let mut my_count = 0i32;
    let mut opp_count = 0i32;
    let mut empty = 0i32;
    for r in 0..SIZE {
        for c in 0..SIZE {
            let v = board[idx(r, c)];
            if v == player {
                weight_score += WEIGHTS[r][c];
                my_count += 1;
            } else if v == opp {
                weight_score -= WEIGHTS[r][c];
                opp_count += 1;
            } else {
                empty += 1;
            }
        }
    }

    // Mobility スコア（全フェーズ共通）
    let my_moves = count_valid_moves(board, player);
    let opp_moves = count_valid_moves(board, opp);
    let mobility = my_moves - opp_moves;

    if empty > 44 {
        // 序盤: Mobility を強調
        return weight_score + mobility * 20;
    }

    if empty < 20 {
        // 終盤: 石数差を主成分とする
        return (my_count - opp_count) * 10 + weight_score + mobility * 10;
    }

    // 中盤: Stability + Frontier + Mobility
    let stability_score = (count_stable(board, player) - count_stable(board, opp)) * 25;
    let frontier_score = (count_frontier(board, opp) - count_frontier(board, player)) * 5;
    weight_score + mobility * 10 + stability_score + frontier_score
}

/// 終局評価: 石数差で勝敗を表現し、残り depth を加味して「早い決着」を選好する
/// （evaluator.evaluate_final と同一: 勝ち +(10000+depth) / 負け -(10000+depth) / 引き分け 0）。
fn evaluate_final(board: &Board, player: i8, depth: i32) -> i32 {
    let opp = opponent(player);
    let mut my_count = 0i32;
    let mut opp_count = 0i32;

    for i in 0..(SIZE * SIZE) {
        if board[i] == player {
            my_count += 1;
        } else if board[i] == opp {
            opp_count += 1;
        }
    }

    if my_count > opp_count {
        10000 + depth
    } else if my_count < opp_count {
        -(10000 + depth)
    } else {
        0
    }
}

/// アルファベータ探索の再帰関数（alpha_beta._alpha_beta と同一の挙動）。
/// is_maximizing=true は AI 側（最大化）、false は相手側（最小化）として動作する。
///
/// tt はこの探索呼び出し専用のトランスポジションテーブル。呼び出し元（best_move /
/// best_move_timed の深さ1段）が新規作成したものをそのまま渡す（呼び出しをまたいで永続化しない）。
fn alpha_beta(
    board: &Board,
    depth: i32,
    mut alpha: i32,
    mut beta: i32,
    is_maximizing: bool,
    ai_player: i8,
    tt: &mut TT,
) -> i32 {
    // TT 参照: 十分な深さで探索済みのエントリがあれば再探索を省略する
    // （C# 版 AlphaBetaAI.AlphaBeta と同じ順序・意味論）。
    let key: TTKey = (zobrist_hash(board), is_maximizing);
    if let Some(&(cached_score, cached_depth, node_type)) = tt.get(&key) {
        if cached_depth >= depth {
            match node_type {
                NodeType::Exact => return cached_score,
                NodeType::LowerBound => alpha = alpha.max(cached_score),
                NodeType::UpperBound => beta = beta.min(cached_score),
            }
            if alpha >= beta {
                return cached_score;
            }
        }
    }

    // 深さ 0 → 評価関数で打ち切る（手生成より前に判定して無駄を省く）
    if depth == 0 {
        let value = evaluate(board, ai_player);
        tt.insert(key, (value, depth, NodeType::Exact));
        return value;
    }

    let current = if is_maximizing {
        ai_player
    } else {
        opponent(ai_player)
    };
    let mut moves = get_valid_moves(board, current);

    if moves.is_empty() {
        // 現在のプレイヤーに有効手がない場合のみ相手の有効手を調べる
        if !has_any_valid_move(board, opponent(current)) {
            // 両者パス → 終局評価（残り depth を渡して早い決着を選好）
            let value = evaluate_final(board, ai_player, depth);
            tt.insert(key, (value, depth, NodeType::Exact));
            return value;
        }
        // パス: 深さを 1 減らし is_maximizing を反転して相手にターンを渡す
        // パスは分岐がなく窓の影響を受けないため、常に Exact として格納してよい。
        let value = alpha_beta(board, depth - 1, alpha, beta, !is_maximizing, ai_player, tt);
        tt.insert(key, (value, depth, NodeType::Exact));
        return value;
    }

    // ムーブオーダリング: 位置重み降順の安定ソート（Python の list.sort(reverse=True) と一致）
    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));
    // node_type 判定用に窓の元の値を退避する（ループ内で alpha/beta が更新されるため）。
    let original_alpha = alpha;
    let original_beta = beta;

    let value = if is_maximizing {
        let mut value = NEG_INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.max(alpha_beta(
                &next,
                depth - 1,
                alpha,
                beta,
                false,
                ai_player,
                tt,
            ));
            alpha = alpha.max(value);
            if alpha >= beta {
                break; // ベータカット
            }
        }
        value
    } else {
        let mut value = INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.min(alpha_beta(
                &next,
                depth - 1,
                alpha,
                beta,
                true,
                ai_player,
                tt,
            ));
            beta = beta.min(value);
            if alpha >= beta {
                break; // アルファカット
            }
        }
        value
    };

    // node_type の判定は退避しておいた original_alpha/original_beta と比較する
    // （ループ内で更新された alpha/beta と比較すると fail-high/fail-low の境界値を
    // 誤って Exact 扱いしてしまうバグになる。C# 版のコメントに残る F2 と同種のバグ）。
    let node_type = if value <= original_alpha {
        NodeType::UpperBound
    } else if value >= original_beta {
        NodeType::LowerBound
    } else {
        NodeType::Exact
    };
    tt.insert(key, (value, depth, node_type));
    value
}

/// 指定盤面・プレイヤー・探索深さで最善手を返す（alpha_beta.get_best_move と同一）。
/// 有効手がない場合は None を返す（呼び出し元でパス処理が必要）。
fn best_move(board: &Board, player: i8, depth: i32) -> Option<(usize, usize)> {
    let mut moves = get_valid_moves(board, player);
    if moves.is_empty() {
        return None;
    }

    // ムーブオーダリングで枝刈り効率を上げる（再帰内と同じ降順安定ソート）
    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));

    let mut best = moves[0]; // デフォルトは最初の候補
    let mut best_score = NEG_INF;
    let mut alpha = NEG_INF;
    let beta = INF;
    // TT はこの呼び出し（1 回の探索）専用。呼び出しをまたいで永続化しない
    // （C# 版 GetBestMoveFixedDepth と同じ設計。メモリ上限や eviction が不要になる）。
    let mut tt: TT = HashMap::new();

    for (r, c) in moves {
        let next = make_move(board, r, c, player);
        // player は最大化側 → 次は最小化（is_maximizing=false）
        let score = alpha_beta(&next, depth - 1, alpha, beta, false, player, &mut tt);
        if score > best_score {
            // 同点は先に出た（＝位置重みの高い）手を維持（Python の > と一致）
            best_score = score;
            best = (r, c);
        }
        alpha = alpha.max(best_score);
    }

    Some(best)
}

/// タイムアウト検出用の Result 型エイリアス（Err(()) = 時間切れ）。
type TimedResult = Result<i32, ()>;

/// 時間制限付きアルファベータ探索（alpha_beta と同一ロジック、deadline 超過で Err(()) を返す）。
/// tt の意味論は alpha_beta と同一。
fn alpha_beta_timed(
    board: &Board,
    depth: i32,
    mut alpha: i32,
    mut beta: i32,
    is_maximizing: bool,
    ai_player: i8,
    deadline: Instant,
    tt: &mut TT,
) -> TimedResult {
    if Instant::now() >= deadline {
        return Err(());
    }

    let key: TTKey = (zobrist_hash(board), is_maximizing);
    if let Some(&(cached_score, cached_depth, node_type)) = tt.get(&key) {
        if cached_depth >= depth {
            match node_type {
                NodeType::Exact => return Ok(cached_score),
                NodeType::LowerBound => alpha = alpha.max(cached_score),
                NodeType::UpperBound => beta = beta.min(cached_score),
            }
            if alpha >= beta {
                return Ok(cached_score);
            }
        }
    }

    if depth == 0 {
        let value = evaluate(board, ai_player);
        tt.insert(key, (value, depth, NodeType::Exact));
        return Ok(value);
    }

    let current = if is_maximizing {
        ai_player
    } else {
        opponent(ai_player)
    };
    let mut moves = get_valid_moves(board, current);

    if moves.is_empty() {
        if !has_any_valid_move(board, opponent(current)) {
            let value = evaluate_final(board, ai_player, depth);
            tt.insert(key, (value, depth, NodeType::Exact));
            return Ok(value);
        }
        let value = alpha_beta_timed(
            board,
            depth - 1,
            alpha,
            beta,
            !is_maximizing,
            ai_player,
            deadline,
            tt,
        )?;
        tt.insert(key, (value, depth, NodeType::Exact));
        return Ok(value);
    }

    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));
    let original_alpha = alpha;
    let original_beta = beta;

    let value = if is_maximizing {
        let mut value = NEG_INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.max(alpha_beta_timed(
                &next,
                depth - 1,
                alpha,
                beta,
                false,
                ai_player,
                deadline,
                tt,
            )?);
            alpha = alpha.max(value);
            if alpha >= beta {
                break;
            }
        }
        value
    } else {
        let mut value = INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.min(alpha_beta_timed(
                &next,
                depth - 1,
                alpha,
                beta,
                true,
                ai_player,
                deadline,
                tt,
            )?);
            beta = beta.min(value);
            if alpha >= beta {
                break;
            }
        }
        value
    };

    let node_type = if value <= original_alpha {
        NodeType::UpperBound
    } else if value >= original_beta {
        NodeType::LowerBound
    } else {
        NodeType::Exact
    };
    tt.insert(key, (value, depth, node_type));
    Ok(value)
}

/// 反復深化探索（Iterative Deepening）で最善手を返す（get_best_move_timed の内部実装）。
/// 深さ 1 から max_depth まで繰り返し探索し、time_ms 以内に完了した最深の結果を返す。
fn best_move_timed(
    board: &Board,
    player: i8,
    max_depth: i32,
    time_ms: u64,
) -> Option<(usize, usize)> {
    let mut moves = get_valid_moves(board, player);
    if moves.is_empty() {
        return None;
    }

    let deadline = Instant::now() + Duration::from_millis(time_ms);
    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));

    // 最低限の初期値（深さ 1 の完了で必ず上書きされる）
    let mut best = moves[0];

    'outer: for depth in 1..=max_depth {
        if Instant::now() >= deadline {
            break;
        }

        let mut current_best = moves[0];
        let mut current_best_score = NEG_INF;
        let mut alpha = NEG_INF;
        let beta = INF;
        // 深さごとに TT を作り直す（C# 版 GetBestMoveIterativeDeepening と同じ。
        // 深さをまたいだ TT 再利用は行わない＝安全側）。
        let mut tt: TT = HashMap::new();

        for &(r, c) in &moves {
            let next = make_move(board, r, c, player);
            match alpha_beta_timed(
                &next,
                depth - 1,
                alpha,
                beta,
                false,
                player,
                deadline,
                &mut tt,
            ) {
                Ok(score) => {
                    if score > current_best_score {
                        current_best_score = score;
                        current_best = (r, c);
                    }
                    alpha = alpha.max(current_best_score);
                }
                Err(()) => {
                    // 探索途中でタイムアウト → この深さの結果は破棄して前の深さの結果を使う
                    break 'outer;
                }
            }
        }

        best = current_best;
    }

    Some(best)
}

// ===== PyO3 バインディング（python フィーチャ有効時のみコンパイル） =====

/// Python へ公開する最善手計算関数。
///
/// 引数:
///   board  : 8×8 の int 配列（0=Empty, 1=Black, 2=White）
///   player : AI が担当するプレイヤーの色（1=Black, 2=White）
///   depth  : アルファベータ探索の最大深さ
///
/// 戻り値: 最善手 (row, col) のタプル。有効手がない場合は None。
#[cfg(feature = "python")]
#[pyfunction]
fn get_best_move(board: Vec<Vec<i8>>, player: i8, depth: i32) -> PyResult<Option<(usize, usize)>> {
    // プレイヤー値の検証（board.py の opponent が投げる ValueError と整合させる）
    if player != 1 && player != 2 {
        return Err(PyValueError::new_err(format!(
            "Invalid player value: {player}. Must be BLACK(1) or WHITE(2)."
        )));
    }

    // 盤面サイズの検証
    if board.len() != SIZE || board.iter().any(|row| row.len() != SIZE) {
        return Err(PyValueError::new_err("board must be 8x8"));
    }

    // list[list[int]] をフラットな内部盤面へ変換する
    let mut internal: Board = [EMPTY; SIZE * SIZE];
    for r in 0..SIZE {
        for c in 0..SIZE {
            internal[idx(r, c)] = board[r][c];
        }
    }

    Ok(best_move(&internal, player, depth))
}

/// Python へ公開する反復深化最善手計算関数。
///
/// 引数:
///   board     : 8×8 の int 配列（0=Empty, 1=Black, 2=White）
///   player    : AI が担当するプレイヤーの色（1=Black, 2=White）
///   max_depth : 反復深化の最大探索深さ
///   time_ms   : 時間制限（ミリ秒）
///
/// 戻り値: 最善手 (row, col) のタプル。有効手がない場合は None。
#[cfg(feature = "python")]
#[pyfunction]
fn get_best_move_timed(
    board: Vec<Vec<i8>>,
    player: i8,
    max_depth: i32,
    time_ms: u64,
) -> PyResult<Option<(usize, usize)>> {
    if player != 1 && player != 2 {
        return Err(PyValueError::new_err(format!(
            "Invalid player value: {player}. Must be BLACK(1) or WHITE(2)."
        )));
    }

    if board.len() != SIZE || board.iter().any(|row| row.len() != SIZE) {
        return Err(PyValueError::new_err("board must be 8x8"));
    }

    let mut internal: Board = [EMPTY; SIZE * SIZE];
    for r in 0..SIZE {
        for c in 0..SIZE {
            internal[idx(r, c)] = board[r][c];
        }
    }

    Ok(best_move_timed(&internal, player, max_depth, time_ms))
}

/// Python 拡張モジュール othello_ai_rust の定義。
#[cfg(feature = "python")]
#[pymodule]
fn othello_ai_rust(m: &Bound<'_, PyModule>) -> PyResult<()> {
    m.add_function(wrap_pyfunction!(get_best_move, m)?)?;
    m.add_function(wrap_pyfunction!(get_best_move_timed, m)?)?;
    Ok(())
}

// ===== 単体テスト（`cargo test --no-default-features` で実行 / Python 非依存） =====

#[cfg(test)]
mod tests {
    use super::*;

    const BLACK: i8 = 1;
    const WHITE: i8 = 2;

    /// オセロ標準初期配置の盤面を作る。
    fn initial_board() -> Board {
        let mut b: Board = [EMPTY; SIZE * SIZE];
        b[idx(3, 3)] = WHITE;
        b[idx(3, 4)] = BLACK;
        b[idx(4, 3)] = BLACK;
        b[idx(4, 4)] = WHITE;
        b
    }

    /// 全マスを指定色で埋めた盤面を作る。
    fn filled_board(color: i8) -> Board {
        [color; SIZE * SIZE]
    }

    #[test]
    fn opponent_swaps_colors() {
        assert_eq!(opponent(BLACK), WHITE);
        assert_eq!(opponent(WHITE), BLACK);
    }

    #[test]
    fn collect_flips_finds_flipped_piece() {
        // 初期盤面で黒が (2,3) に置くと白 (3,3) が反転する
        let board = initial_board();
        let mut flips = Vec::new();
        collect_flips(&board, 2, 3, BLACK, &mut flips);
        assert!(flips.contains(&idx(3, 3)));
    }

    #[test]
    fn collect_flips_empty_on_isolated_cell() {
        // 隅 (0,0) では何も挟めない
        let board = initial_board();
        let mut flips = Vec::new();
        collect_flips(&board, 0, 0, BLACK, &mut flips);
        assert!(flips.is_empty());
    }

    #[test]
    fn initial_board_black_has_four_moves() {
        let board = initial_board();
        let mut moves = get_valid_moves(&board, BLACK);
        moves.sort();
        let mut expected = vec![(2, 3), (3, 2), (4, 5), (5, 4)];
        expected.sort();
        assert_eq!(moves, expected);
    }

    #[test]
    fn has_any_flip_matches_collect_flips() {
        // 全空きマス×両プレイヤーで has_any_flip と collect_flips の可否が一致する
        let board = initial_board();
        for &player in &[BLACK, WHITE] {
            for r in 0..SIZE {
                for c in 0..SIZE {
                    if board[idx(r, c)] != EMPTY {
                        continue;
                    }
                    let mut flips = Vec::new();
                    collect_flips(&board, r as i32, c as i32, player, &mut flips);
                    assert_eq!(
                        !flips.is_empty(),
                        has_any_flip(&board, r as i32, c as i32, player),
                        "mismatch at ({r},{c}) player={player}"
                    );
                }
            }
        }
    }

    #[test]
    fn count_valid_moves_matches_get_valid_moves() {
        let boards = [initial_board(), make_move(&initial_board(), 2, 3, BLACK)];
        for board in &boards {
            for &player in &[BLACK, WHITE] {
                assert_eq!(
                    count_valid_moves(board, player),
                    get_valid_moves(board, player).len() as i32
                );
            }
        }
    }

    #[test]
    fn make_move_places_and_flips() {
        let board = initial_board();
        let new_board = make_move(&board, 2, 3, BLACK);
        assert_eq!(new_board[idx(2, 3)], BLACK);
        assert_eq!(new_board[idx(3, 3)], BLACK);
    }

    #[test]
    fn make_move_keeps_original_unchanged() {
        let board = initial_board();
        let _ = make_move(&board, 2, 3, BLACK);
        assert_eq!(board[idx(2, 3)], EMPTY);
        assert_eq!(board[idx(3, 3)], WHITE);
    }

    #[test]
    fn count_stable_corner_is_stable() {
        // コーナーに置かれた石は安定石としてカウントされる
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(0, 0)] = BLACK;
        assert!(count_stable(&board, BLACK) >= 1);
    }

    #[test]
    fn count_stable_isolated_center_is_zero() {
        // 孤立した中央の石は安定石ではない
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(3, 3)] = BLACK;
        assert_eq!(count_stable(&board, BLACK), 0);
    }

    #[test]
    fn count_stable_full_top_edge_all_stable() {
        // 上辺を黒で埋めると全 8 マスが安定石になる
        let mut board: Board = [EMPTY; SIZE * SIZE];
        for c in 0..SIZE {
            board[idx(0, c)] = BLACK;
        }
        assert_eq!(count_stable(&board, BLACK), 8);
    }

    #[test]
    fn count_stable_opponent_corner_not_counted() {
        // 相手コーナーは自分の安定石に含まれない
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(0, 0)] = WHITE;
        assert_eq!(count_stable(&board, BLACK), 0);
    }

    #[test]
    fn count_stable_all_same_color_all_stable() {
        // 全マス同色で 64 石が安定石
        let board = filled_board(BLACK);
        assert_eq!(count_stable(&board, BLACK), 64);
    }

    #[test]
    fn count_frontier_empty_board_is_zero() {
        let board: Board = [EMPTY; SIZE * SIZE];
        assert_eq!(count_frontier(&board, BLACK), 0);
    }

    #[test]
    fn count_frontier_isolated_piece_is_one() {
        // 中央の孤立石は全方向に空きがある → フロンティア 1
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(4, 4)] = BLACK;
        assert_eq!(count_frontier(&board, BLACK), 1);
    }

    #[test]
    fn count_frontier_opponent_not_counted() {
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(4, 4)] = WHITE;
        assert_eq!(count_frontier(&board, BLACK), 0);
    }

    #[test]
    fn evaluate_favors_corner_owner() {
        let mut board: Board = [EMPTY; SIZE * SIZE];
        board[idx(0, 0)] = BLACK;
        assert!(evaluate(&board, BLACK) > 0);
        assert!(evaluate(&board, WHITE) < 0);
    }

    #[test]
    fn evaluate_final_win_lose_draw() {
        let win = filled_board(BLACK);
        assert_eq!(evaluate_final(&win, BLACK, 0), 10000);
        assert_eq!(evaluate_final(&win, WHITE, 0), -10000);

        // 上半分 黒・下半分 白の同数盤面
        let mut draw: Board = [EMPTY; SIZE * SIZE];
        for r in 0..SIZE {
            for c in 0..SIZE {
                draw[idx(r, c)] = if r < 4 { BLACK } else { WHITE };
            }
        }
        assert_eq!(evaluate_final(&draw, BLACK, 0), 0);
    }

    #[test]
    fn evaluate_final_prefers_faster_decision() {
        let win = filled_board(BLACK);
        assert!(evaluate_final(&win, BLACK, 5) > evaluate_final(&win, BLACK, 1));
        assert_eq!(evaluate_final(&win, BLACK, 5), 10005);
        assert!(evaluate_final(&win, WHITE, 5) < evaluate_final(&win, WHITE, 1));
        assert_eq!(evaluate_final(&win, WHITE, 5), -10005);
    }

    #[test]
    fn best_move_returns_none_without_valid_moves() {
        // 全マス黒 → 白に有効手なし
        let full = filled_board(BLACK);
        assert_eq!(best_move(&full, WHITE, 3), None);
    }

    #[test]
    fn best_move_returns_legal_move() {
        let board = initial_board();
        let mv = best_move(&board, BLACK, 3).expect("should find a move");
        let legal = get_valid_moves(&board, BLACK);
        assert!(legal.contains(&mv));
    }

    #[test]
    fn best_move_timed_returns_none_without_valid_moves() {
        let full = filled_board(BLACK);
        assert_eq!(best_move_timed(&full, WHITE, 3, 5000), None);
    }

    #[test]
    fn best_move_timed_returns_legal_move() {
        let board = initial_board();
        let mv = best_move_timed(&board, BLACK, 5, 5000).expect("should find a move");
        let legal = get_valid_moves(&board, BLACK);
        assert!(legal.contains(&mv));
    }

    #[test]
    fn best_move_timed_respects_time_limit() {
        // 極端に短い制限時間でも有効手を返すことを確認する（深さ 1 で少なくとも結果を出す）
        let board = initial_board();
        let mv = best_move_timed(&board, BLACK, 10, 1);
        assert!(mv.is_some());
        let legal = get_valid_moves(&board, BLACK);
        assert!(legal.contains(&mv.unwrap()));
    }

    #[test]
    fn best_move_handles_forced_opponent_pass() {
        // (0,0)/(7,7) が空き、(0,1)/(7,6) が白、その他すべて黒 → 白は有効手なし
        let mut board = filled_board(BLACK);
        board[idx(0, 0)] = EMPTY;
        board[idx(0, 1)] = WHITE;
        board[idx(7, 7)] = EMPTY;
        board[idx(7, 6)] = WHITE;

        let mv = best_move(&board, BLACK, 4).expect("should find a move");
        assert!(mv == (0, 0) || mv == (7, 7));
    }

    // ===== トランスポジションテーブル（TT）意味論のテスト =====
    // tt に手動でエントリを仕込み、alpha_beta を直接呼び出して C# 版と同じ意味論
    // （EXACT / LOWER_BOUND / UPPER_BOUND の扱い）であることを検証する。

    #[test]
    fn tt_exact_entry_with_sufficient_depth_is_returned_without_research() {
        let board = initial_board();
        let key = (zobrist_hash(&board), true);
        let fake_score = 999_999;
        let mut tt: TT = HashMap::new();
        tt.insert(key, (fake_score, 10, NodeType::Exact));

        let score = alpha_beta(&board, 3, NEG_INF, INF, true, BLACK, &mut tt);

        assert_eq!(score, fake_score);
    }

    #[test]
    fn tt_entry_with_insufficient_depth_is_ignored() {
        let board = initial_board();
        let key = (zobrist_hash(&board), true);
        let fake_score = 999_999;
        let mut tt: TT = HashMap::new();
        tt.insert(key, (fake_score, 0, NodeType::Exact)); // depth=0 < 要求 depth=2

        let score = alpha_beta(&board, 2, NEG_INF, INF, true, BLACK, &mut tt);

        assert_ne!(score, fake_score);
    }

    #[test]
    fn tt_lower_bound_entry_only_narrows_alpha_not_returned_directly() {
        let board = initial_board();
        let key = (zobrist_hash(&board), true);
        let fake_bound = -999_999;
        let mut tt: TT = HashMap::new();
        tt.insert(key, (fake_bound, 10, NodeType::LowerBound));

        let score = alpha_beta(&board, 3, NEG_INF, INF, true, BLACK, &mut tt);

        assert_ne!(score, fake_bound);
    }

    #[test]
    fn tt_upper_bound_entry_only_narrows_beta_not_returned_directly() {
        let board = initial_board();
        let key = (zobrist_hash(&board), true);
        let fake_bound = 999_999;
        let mut tt: TT = HashMap::new();
        tt.insert(key, (fake_bound, 10, NodeType::UpperBound));

        let score = alpha_beta(&board, 3, NEG_INF, INF, true, BLACK, &mut tt);

        assert_ne!(score, fake_bound);
    }

    #[test]
    fn tt_cached_lower_bound_causes_immediate_cutoff_when_alpha_exceeds_beta() {
        let board = initial_board();
        let key = (zobrist_hash(&board), true);
        let cached_score = 50;
        let mut tt: TT = HashMap::new();
        tt.insert(key, (cached_score, 10, NodeType::LowerBound));

        // beta を cached_score 以下に設定 → alpha が cached_score まで上がった時点で alpha >= beta
        let score = alpha_beta(&board, 3, NEG_INF, cached_score, true, BLACK, &mut tt);

        assert_eq!(score, cached_score);
    }

    #[test]
    fn best_move_with_tt_still_handles_forced_opponent_pass() {
        // TT 導入後も強制パス局面で例外なく合法手が返ることを確認する（回帰）。
        let mut board = filled_board(BLACK);
        board[idx(0, 0)] = EMPTY;
        board[idx(0, 1)] = WHITE;
        board[idx(7, 7)] = EMPTY;
        board[idx(7, 6)] = WHITE;

        let mv = best_move(&board, BLACK, 4).expect("should find a move");
        assert!(mv == (0, 0) || mv == (7, 7));
    }
}
