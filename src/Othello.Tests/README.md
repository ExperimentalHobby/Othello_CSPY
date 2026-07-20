# Othello.Tests

`Othello.Core` のゲームロジック全体を検証する xUnit テストプロジェクト。

## 実行方法

```bash
# 全テスト実行
dotnet test

# 詳細出力
dotnet test --verbosity detailed

# runsettings を明示指定する場合
dotnet test --settings .runsettings
```

## テスト内訳

### C# テスト（合計 292 件）

| グループ | テスト数 | 内容 |
|---------|---------|------|
| AI | 54 | AlphaBetaAI（探索・TT・反復深化）/ DifficultyLevel 拡張 / Evaluator（フェーズ切替・Stability・Frontier）/ PythonSubprocessAI（単体・結合） |
| Console | 13 | ConsoleInputParser の入力パース |
| Core/Game | 17 | GameEngine の着手・Pass・Undo・終局処理・FlippedPieces・GetResult 勝敗 |
| Core/Models | 38 | Board / Position / PlayerColor / GameState / MoveResult |
| Core/Rules | 22 | OthelloRules / FlipCalculator / GetGameResult |
| Kifu | 33 | KifuPlayer・KifuSerializer・KifuViewModel・GameViewModel の棋譜保存・再生連携 |
| Settings | 4 | OthelloSettingsManager の設定読み書き |
| Stats | 23 | GameStats・StatsViewModel の統計集計・表示 |
| ViewModels | 88 | GameViewModel（AI 連携・設定変更・Undo・CanExecute・エラー・クリック制御）/ BoardSquareViewModel / CpuVsCpu / ScoreHistory / TimeLimit |

### Python テスト（合計 69 件）

| グループ | テスト数 | 内容 |
|---------|---------|------|
| board.py | 10 | opponent / get_flips / has_any_flip / get_valid_moves / count_valid_moves / make_move |
| evaluator.py | 19 | evaluate（フェーズ切替・コーナー・X-square・辺・Mobility）/ evaluate_final / count_stable / count_frontier |
| alpha_beta | 16 | AlphaBetaAI.get_best_move（固定深さ・反復深化・強制パス局面）/ トランスポジションテーブル意味論 |
| opening_book.py | 12 | 定石 lookup・対称変換（回転・鏡映）・build_book の合法性検証 |
| IPC 結合 | 10 | ai.py サブプロセスの JSON 往復・連続リクエスト・エラー応答・定石参照フォールバック |
| Rust 整合性 | 2 | Rust 拡張と純 Python の着手一致確認（Rust 未ビルド時は自動スキップ） |

## テスト設定（.runsettings）

リポジトリルートの `.runsettings` でテスト実行を制御している。

| 設定 | 値 | 内容 |
|------|-----|------|
| `TestSessionTimeout` | 30000 ms | テストセッション全体の上限時間 |
| `MaxCpuCount` | 0（全コア） | 並列実行に使用する CPU コア数 |
| `ParallelizeAssembly / TestClasses` | true | アセンブリ・クラス単位の並列化 |
| `CodeCoverage → ModulePath` | `Othello.Core` | カバレッジ収集対象を Core に限定 |

## Python AI のテスト

C# のテストには含まれない Python AI（board / evaluator / alpha_beta / opening_book / IPC）は、
標準ライブラリ `unittest` による単体・結合テスト（`src/Othello.AI/Python/test_othello.py` など）で検証する。
Rust 拡張と純 Python の挙動一致は `test_parity.py` で確認する（Rust 未ビルド時は自動スキップ）。

```bash
# Python テスト（リポジトリルートから実行）
py -m unittest discover -s src/Othello.AI/Python -p "test_*.py"
```

stdin/stdout の IPC を手動確認したい場合は以下のコマンドを使う。

```bash
echo '{"board":[[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,2,1,0,0,0],[0,0,0,1,2,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0]],"player":1,"depth":5}' | py src/Othello.AI/Python/ai.py
```

## Rust AI のテスト

C# / Python のテストには含まれない Rust ロジック（has_any_flip / collect_flips / make_move / evaluate / evaluate_final / count_stable / count_frontier / alpha_beta / トランスポジションテーブル など）は、`src/Othello.AI/Rust/src/lib.rs` の `#[cfg(test)]` モジュールに単体テストを直接記述している。

| テスト名 | 内容 |
|---------|------|
| `opponent_swaps_colors` | 相手色の反転 |
| `collect_flips_finds_flipped_piece` | 反転石の収集 |
| `collect_flips_empty_on_isolated_cell` | 反転なし（孤立セル） |
| `initial_board_black_has_four_moves` | 初期有効手が 4 件 |
| `has_any_flip_matches_collect_flips` | 短絡評価と全列挙の一致 |
| `count_valid_moves_matches_get_valid_moves` | mobility カウントの一致 |
| `make_move_places_and_flips` | 着手・反転処理 |
| `make_move_keeps_original_unchanged` | イミュータブル盤面 |
| `count_stable_corner_is_stable` | コーナーの石は安定石になる |
| `count_stable_isolated_center_is_zero` | 孤立した中央石は不安定 |
| `count_stable_full_top_edge_all_stable` | 上辺を埋めると全 8 マスが安定石になる |
| `count_stable_opponent_corner_not_counted` | 相手コーナーは自分の安定石に含まれない |
| `count_stable_all_same_color_all_stable` | 全マス同色なら 64 石すべて安定石 |
| `count_frontier_empty_board_is_zero` | 空盤面ではフロンティア 0 |
| `count_frontier_isolated_piece_is_one` | 孤立した中央石はフロンティア 1 |
| `count_frontier_opponent_not_counted` | 相手の石はフロンティアにカウントしない |
| `evaluate_favors_corner_owner` | コーナー所有者を高く評価 |
| `evaluate_final_win_lose_draw` | 終局評価の勝ち・負け・引き分け |
| `evaluate_final_prefers_faster_decision` | 早い決着ほど高く（低く）評価 |
| `best_move_returns_none_without_valid_moves` | 有効手なし → None |
| `best_move_returns_legal_move` | 返り手が合法手 |
| `best_move_timed_returns_none_without_valid_moves` | 反復深化・有効手なし → None |
| `best_move_timed_returns_legal_move` | 反復深化・返り手が合法手 |
| `best_move_timed_respects_time_limit` | 極端に短い制限時間でも結果を返す |
| `best_move_handles_forced_opponent_pass` | 相手強制パス局面での着手 |
| `tt_exact_entry_with_sufficient_depth_is_returned_without_research` | 十分な depth の EXACT エントリをそのまま返す |
| `tt_entry_with_insufficient_depth_is_ignored` | depth 不足のエントリは無視される |
| `tt_lower_bound_entry_only_narrows_alpha_not_returned_directly` | LOWER_BOUND は alpha を狭めるだけで直接返さない |
| `tt_upper_bound_entry_only_narrows_beta_not_returned_directly` | UPPER_BOUND は beta を狭めるだけで直接返さない |
| `tt_cached_lower_bound_causes_immediate_cutoff_when_alpha_exceeds_beta` | キャッシュされた境界値で alpha ≥ beta になったら即座にカットオフ |
| `best_move_with_tt_still_handles_forced_opponent_pass` | TT 導入後も強制パス局面で例外なく合法手を返す（回帰） |

```bash
# Rust テスト（リポジトリルートから実行）
cargo test --manifest-path src/Othello.AI/Rust/Cargo.toml --no-default-features
```
