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
use std::time::{Duration, Instant};

/// 盤面の一辺のサイズ（オセロは常に 8×8）
const SIZE: usize = 8;
/// 空きマスを表す値（Python / C# の PlayerColor.Empty と一致）
const EMPTY: i8 = 0;

/// 探索する 8 方向のベクトル (dRow, dCol)。board.py の DIRS と同順。
const DIRS: [(i32, i32); 8] = [
    (-1, -1), (-1, 0), (-1, 1),
    (0, -1),           (0, 1),
    (1, -1),  (1, 0),  (1, 1),
];

/// 盤面の位置重みテーブル（evaluator.py の WEIGHTS と同一）。
const WEIGHTS: [[i32; SIZE]; SIZE] = [
    [100, -20, 10,  5,  5, 10, -20, 100],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [ 10,  -2,  5,  1,  1,  5,  -2,  10],
    [  5,  -2,  1,  2,  2,  1,  -2,   5],
    [  5,  -2,  1,  2,  2,  1,  -2,   5],
    [ 10,  -2,  5,  1,  1,  5,  -2,  10],
    [-20, -50, -2, -2, -2, -2, -50, -20],
    [100, -20, 10,  5,  5, 10, -20, 100],
];

/// アルファベータ探索の境界に用いる擬似無限大（Python の float('inf') 相当）。
const INF: i32 = i32::MAX;
const NEG_INF: i32 = i32::MIN;

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
        while nr >= 0 && nr < SIZE as i32 && nc >= 0 && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == opp
        {
            line.push(idx(nr as usize, nc as usize));
            nr += dr;
            nc += dc;
        }

        // 連続した相手石の先に自分の石があれば反転確定
        if !line.is_empty()
            && nr >= 0 && nr < SIZE as i32 && nc >= 0 && nc < SIZE as i32
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

        while nr >= 0 && nr < SIZE as i32 && nc >= 0 && nc < SIZE as i32
            && board[idx(nr as usize, nc as usize)] == opp
        {
            seen_opp = true;
            nr += dr;
            nc += dc;
        }

        if seen_opp
            && nr >= 0 && nr < SIZE as i32 && nc >= 0 && nc < SIZE as i32
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

/// 中盤評価: 位置重みの差 + mobility 差（×10）を player 視点で返す（evaluator.evaluate と同一）。
fn evaluate(board: &Board, player: i8) -> i32 {
    let opp = opponent(player);
    let mut score = 0i32;

    for r in 0..SIZE {
        for c in 0..SIZE {
            let v = board[idx(r, c)];
            if v == player {
                score += WEIGHTS[r][c];
            } else if v == opp {
                score -= WEIGHTS[r][c];
            }
        }
    }

    let my_moves = count_valid_moves(board, player);
    let opp_moves = count_valid_moves(board, opp);
    score += (my_moves - opp_moves) * 10;

    score
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
fn alpha_beta(
    board: &Board,
    depth: i32,
    mut alpha: i32,
    mut beta: i32,
    is_maximizing: bool,
    ai_player: i8,
) -> i32 {
    // 深さ 0 → 評価関数で打ち切る（手生成より前に判定して無駄を省く）
    if depth == 0 {
        return evaluate(board, ai_player);
    }

    let current = if is_maximizing { ai_player } else { opponent(ai_player) };
    let mut moves = get_valid_moves(board, current);

    if moves.is_empty() {
        // 現在のプレイヤーに有効手がない場合のみ相手の有効手を調べる
        if !has_any_valid_move(board, opponent(current)) {
            // 両者パス → 終局評価（残り depth を渡して早い決着を選好）
            return evaluate_final(board, ai_player, depth);
        }
        // パス: 深さを 1 減らし is_maximizing を反転して相手にターンを渡す
        return alpha_beta(board, depth - 1, alpha, beta, !is_maximizing, ai_player);
    }

    // ムーブオーダリング: 位置重み降順の安定ソート（Python の list.sort(reverse=True) と一致）
    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));

    if is_maximizing {
        let mut value = NEG_INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.max(alpha_beta(&next, depth - 1, alpha, beta, false, ai_player));
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
            value = value.min(alpha_beta(&next, depth - 1, alpha, beta, true, ai_player));
            beta = beta.min(value);
            if alpha >= beta {
                break; // アルファカット
            }
        }
        value
    }
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

    for (r, c) in moves {
        let next = make_move(board, r, c, player);
        // player は最大化側 → 次は最小化（is_maximizing=false）
        let score = alpha_beta(&next, depth - 1, alpha, beta, false, player);
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
fn alpha_beta_timed(
    board: &Board,
    depth: i32,
    mut alpha: i32,
    mut beta: i32,
    is_maximizing: bool,
    ai_player: i8,
    deadline: Instant,
) -> TimedResult {
    if Instant::now() >= deadline {
        return Err(());
    }

    if depth == 0 {
        return Ok(evaluate(board, ai_player));
    }

    let current = if is_maximizing { ai_player } else { opponent(ai_player) };
    let mut moves = get_valid_moves(board, current);

    if moves.is_empty() {
        if !has_any_valid_move(board, opponent(current)) {
            return Ok(evaluate_final(board, ai_player, depth));
        }
        return alpha_beta_timed(board, depth - 1, alpha, beta, !is_maximizing, ai_player, deadline);
    }

    moves.sort_by(|a, b| WEIGHTS[b.0][b.1].cmp(&WEIGHTS[a.0][a.1]));

    if is_maximizing {
        let mut value = NEG_INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.max(alpha_beta_timed(&next, depth - 1, alpha, beta, false, ai_player, deadline)?);
            alpha = alpha.max(value);
            if alpha >= beta {
                break;
            }
        }
        Ok(value)
    } else {
        let mut value = INF;
        for (r, c) in moves {
            let next = make_move(board, r, c, current);
            value = value.min(alpha_beta_timed(&next, depth - 1, alpha, beta, true, ai_player, deadline)?);
            beta = beta.min(value);
            if alpha >= beta {
                break;
            }
        }
        Ok(value)
    }
}

/// 反復深化探索（Iterative Deepening）で最善手を返す（get_best_move_timed の内部実装）。
/// 深さ 1 から max_depth まで繰り返し探索し、time_ms 以内に完了した最深の結果を返す。
fn best_move_timed(board: &Board, player: i8, max_depth: i32, time_ms: u64) -> Option<(usize, usize)> {
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

        let mut current_best       = moves[0];
        let mut current_best_score = NEG_INF;
        let mut alpha              = NEG_INF;
        let beta                   = INF;

        for &(r, c) in &moves {
            let next = make_move(board, r, c, player);
            match alpha_beta_timed(&next, depth - 1, alpha, beta, false, player, deadline) {
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
}
