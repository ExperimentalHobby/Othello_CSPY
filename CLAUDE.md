# CLAUDE.md

このファイルは、本リポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

**人間 vs CPU AI** のオセロ（リバーシ）ゲーム。

- **AI**: Rust（PyO3 拡張）優先、未ビルド時は純 Python にフォールバック。C# ↔ Python ↔ Rust の三層構成で stdin/stdout JSON 通信
- **UI**: C# .NET 10 + WPF / WinUI3（MVVM パターン、共有 ViewModel）
- **コンソール版**: C# .NET 10（ターミナルで対話プレイ）
- **Python**: 3.8 以上（標準ライブラリのみ、追加インストール不要）
- **Rust**: PyO3（`othello_ai_rust.pyd`/.so）でアルファベータ探索を高速化。`alpha_beta.py` がシムとしてバックエンドを自動選択

## C# ↔ Python IPC プロトコル

**C# → Python（stdin 1行 JSON）**
```json
{"board": [[int,...×8],...×8], "player": 1, "depth": 5, "time_ms": null}
```
- `player`: 1=黒, 2=白（AI が担当する色）
- `depth`: 探索深さ（Easy:2, Normal:5, Hard:10）
- `time_ms`: 反復深化の制限時間（ms）。Hard=8000、Easy/Normal=null（固定深さ探索）

**Python → C#（stdout 1行 JSON）**
```json
{"row": 2, "col": 3}
```

Python プロセスのライフサイクル: `StartNewGame()` で起動 → `EndGame()` / `GameViewModel.Dispose()`（ウィンドウ閉鎖時）で停止。

## 作業ルール

- **実装前にプランを提示すること**: コード変更を行う前に、方針・変更箇所・影響範囲を日本語でまとめたプランを提示し、ユーザーの承認を得てから実装に進む。
- **テスト駆動開発を意識すること**: 新機能追加・バグ修正の際は、先にテストを書いてから実装する。既存テストが壊れていないことを確認してからコードを提出する。

## 主要な設計ポイント

### AI 三層構成（C# → Python → Rust）
- `PythonSubprocessAI`（C#）が Python プロセス（`ai.py`）を起動し JSON で通信
- `ai.py` は `alpha_beta.py` 経由でバックエンドを選択する（**シム方式**）
- `alpha_beta.py`: `othello_ai_rust` が import できれば Rust、できなければ `alpha_beta_py.AlphaBetaAI`（純 Python）を使用
- **parity 保証**: `test_parity.py` が Rust 版と Python 版の同一局面・深さでの着手一致を検証する
- Hard 難易度では `time_ms=8000`（8 秒）を IPC で送信し、反復深化（Iterative Deepening）を使用

### GameEngine
- **パスは自動スキップ**: `AdvanceTurn()` が相手に有効手がなければ即座にスキップし `LastPassedPlayer` に記録する。UI/コンソールはこのプロパティを参照してメッセージを表示する。手動 `Pass()` 呼び出しは不要（`Pass()` は `internal` で現 UI は使用しない）
- **Undo はスナップショット方式**: 履歴に `Snapshot(Board, PlayerColor, GameState)` を積む。`Undo()` はスナップショットをそのまま復元するため、パスをまたぐ Undo でも手番・状態が正確に戻る（単純な `_currentPlayer.Opponent()` では不正確になる）
- `GameEngine.IsInitialState`: 履歴が初期スナップショット 1 件のみ = まだ一手も打っていない状態。難易度・色変更の可否判定に使う

### GameViewModel
- **AI 注入パターン**: コンストラクタで `Func<DifficultyLevel, IAIStrategy>? aiFactory` を受け取る。`null` の場合は `CreateDefaultAI`（Python サブプロセス）を使う。テストでは `FakeAI` を差し込める
- `HumanColorIndex` / `DifficultyIndex`: ComboBox と ViewModel を繋ぐ int ヘルパープロパティ
- `Difficulty` / `HumanColor` の setter: `IsInitialState` なら `StartNewGame()` を呼び直すことでソフトロックを防ぐ
- `NotifyIfPassed()`: 着手後に `_engine.LastPassedPlayer` を参照しステータスメッセージに表示する
- `OnUndo()`: AI ターンになった場合はさらに1回 Undo して人間のターンに戻す
- 有効手ハイライト: 人間のターン時のみ黄色でハイライト（`RefreshBoardDisplay` 内）
- キャンセル競合防止: `ProcessAIMoveAsync`・`CheckAndProcessNextTurnAsync` の汎用 `catch` 冒頭で `ct.IsCancellationRequested` を確認し早期 return（新規ゲーム開始時の旧タスク例外が新ゲームを破壊しない）
- **反転アニメーション**: 着手後に `AnimateFlipsAsync(flippedPieces)` を fire-and-forget で呼び、`BoardSquareViewModel.IsBeingFlipped` を使って WPF/WinUI3 に flip 演出を通知する

### PythonSubprocessAI
- `FindPythonExecutable()`: Windows では `py` → `python3` → `python` の順で自動検索
- Python スクリプトのパスは `AiScriptPaths.AiScriptPath`（`Othello.Core.AI`）で一元管理
- Python ファイルは各 `.csproj` の Content ビルドアクションで出力ディレクトリ（`Othello.Python/`）にコピーされる（`test_*.py` は除外）
- `BeginErrorReadLine()` で stderr を非同期ドレイン（`_stderrBuffer` に蓄積）。同期 `ReadToEnd` によるデッドロックを回避する
- `ReadLineWithTimeout()`: `ReadLineAsync().Wait(timeout)` 方式。専用スレッドを生成しない
- Hard 難易度時は `time_ms` を IPC に付加し、Python 側で反復深化探索を使用する

### Python AI
- `has_any_flip(board, r, c, player)`: 挟める方向が 1 つでも見つかれば即 `True` を返す短絡評価。有効手一覧生成の高速化に使う
- `count_valid_moves(board, player)`: 座標リストを構築せず件数のみ返す。`evaluate()` の Mobility 計算に使う
- `evaluate_final(board, player, depth=0)`: 勝利 `+(10000+depth)`・敗北 `-(10000+depth)`。残り探索深さを加味して早い勝ちを選好・負けを先送り
- `AlphaBetaAI.get_best_move_timed(board, player, depth, time_ms)`: 反復深化（深さ 1〜depth を時間制限内で繰り返す）。`time_ms` が `null` のときは `get_best_move` を使用
- `PlayerColor` の int 値（`Empty=0, Black=1, White=2`）は Python JSON の `"player"` フィールドと直接対応しており変更不可

### テスト
- `Othello.Core.csproj` に `InternalsVisibleTo("Othello.Tests")` を設定済み。`GameEngine.LoadStateForTest(board, player)` で任意の境界局面を再現できる
- `Othello.Tests.csproj` は ViewModels 共有プロジェクト（`.projitems`）を直接取り込んでいるため `GameViewModel` をテスト対象にできる
- `ConsoleInputParser`（`Othello.Console` の `internal static`）は `InternalsVisibleTo` 経由でテスト可能
- **単体テスト**: 変更した関数・クラスの正常系・異常系・境界値を網羅する
- **結合テスト**: 変更が影響する他機能（例: GameEngine の変更なら UI の挙動、AI との連携など）についても結合テストを追加し、機能間の連携が壊れていないことを確認する
