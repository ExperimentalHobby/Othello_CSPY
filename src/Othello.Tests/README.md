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

### C# テスト（合計 64 件）

| グループ | テスト数 | 内容 |
|---------|---------|------|
| Models | 18 | Board (6), Position (4), PlayerColor (5), GameState (1), MoveResult (2) |
| Rules | 15 | OthelloRules (9), FlipCalculator (3), GetGameResult (3) |
| Game | 17 | GameEngine の着手・Pass・Undo・終局処理・FlippedPieces・GetResult 勝敗 |
| ViewModels | 14 | GameViewModel の AI 連携・設定変更・Undo・CanExecute・エラー・クリック制御 |

### Python テスト（合計 23〜24 件）

| グループ | テスト数 | 内容 |
|---------|---------|------|
| board.py | 10 | opponent / get_flips / has_any_flip / get_valid_moves / count_valid_moves / make_move |
| evaluator.py | 7 | evaluate（コーナー・X-square・辺・Mobility・公式検証）/ evaluate_final |
| alpha_beta | 3 | AlphaBetaAI.get_best_move（合法手・有効手なし・強制パス局面） |
| IPC 結合 | 3 | ai.py サブプロセスの JSON 往復・連続リクエスト・エラー応答 |
| Rust 整合性 | 1 | Rust 拡張と純 Python の着手一致確認（Rust 未ビルド時は自動スキップ） |

## テスト設定（.runsettings）

リポジトリルートの `.runsettings` でテスト実行を制御している。

| 設定 | 値 | 内容 |
|------|-----|------|
| `TestSessionTimeout` | 30000 ms | テストセッション全体の上限時間 |
| `MaxCpuCount` | 0（全コア） | 並列実行に使用する CPU コア数 |
| `ParallelizeAssembly / TestClasses` | true | アセンブリ・クラス単位の並列化 |
| `CodeCoverage → ModulePath` | `Othello.Core` | カバレッジ収集対象を Core に限定 |

## Python AI のテスト

C# のテストには含まれない Python AI（board / evaluator / alpha_beta / IPC）は、
標準ライブラリ `unittest` による単体・結合テスト（`src/Othello.Python/test_othello.py`）で検証する。
Rust 拡張と純 Python の挙動一致は `test_parity.py` で確認する（Rust 未ビルド時は自動スキップ）。

```bash
# Python テスト（リポジトリルートから実行）
py -m unittest discover -s src/Othello.Python -p "test_*.py"
```

stdin/stdout の IPC を手動確認したい場合は以下のコマンドを使う。

## Rust AI のテスト

C# / Python のテストには含まれない Rust ロジック（has_any_flip / collect_flips / make_move / evaluate / alpha_beta など）は、`src/Othello.Rust/src/lib.rs` の `#[cfg(test)]` モジュールに単体テストを直接記述している。

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
| `evaluate_favors_corner_owner` | コーナー位置の評価優位 |
| `evaluate_final_win_lose_draw` | 終局評価の勝ち・負け・引き分け |
| `evaluate_final_prefers_faster_decision` | 早い決着を選好 |
| `best_move_returns_none_without_valid_moves` | 有効手なし → None |
| `best_move_returns_legal_move` | 返り手が合法手 |
| `best_move_handles_forced_opponent_pass` | 相手強制パス局面での着手 |

```bash
# Rust テスト（リポジトリルートから実行）
cargo test --manifest-path src/Othello.Rust/Cargo.toml --no-default-features
```

stdin/stdout の IPC を手動確認したい場合は以下のコマンドを使う。

```bash
echo '{"board":[[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,2,1,0,0,0],[0,0,0,1,2,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0]],"player":1,"depth":5}' | py src/Othello.Python/ai.py
```
