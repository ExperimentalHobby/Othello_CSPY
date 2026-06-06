# CLAUDE.md

このファイルは、本リポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

**人間 vs Python AI** のオセロ（リバーシ）ゲーム。

- **AI**: Python（alpha-beta 探索）← stdin/stdout JSON で C# と通信
- **UI**: C# .NET 10 + WPF / WinUI3（MVVM パターン、共有 ViewModel）
- **コンソール版**: C# .NET 10（ターミナルで対話プレイ）
- **Python**: 3.8 以上（標準ライブラリのみ、追加インストール不要）

## C# ↔ Python IPC プロトコル

**C# → Python（stdin 1行 JSON）**
```json
{"board": [[int,...×8],...×8], "player": 1, "depth": 5}
```
- `player`: 1=黒, 2=白（AI が担当する色）
- `depth`: 探索深さ（Easy:2, Normal:5, Hard:10）

**Python → C#（stdout 1行 JSON）**
```json
{"row": 2, "col": 3}
```

Python プロセスのライフサイクル: `StartNewGame()` で起動 → `EndGame()` / `GameViewModel.Dispose()`（ウィンドウ閉鎖時）で停止。

## 作業ルール

- **実装前にプランを提示すること**: コード変更を行う前に、方針・変更箇所・影響範囲を日本語でまとめたプランを提示し、ユーザーの承認を得てから実装に進む。
- **テスト駆動開発を意識すること**: 新機能追加・バグ修正の際は、先にテストを書いてから実装する。既存テストが壊れていないことを確認してからコードを提出する。

## 主要な設計ポイント

### GameEngine
- **パスは自動スキップ**: `AdvanceTurn()` が相手に有効手がなければ即座にスキップし `LastPassedPlayer` に記録する。UI/コンソールはこのプロパティを参照してメッセージを表示する。手動 `Pass()` 呼び出しは不要（`Pass()` 自体は有効手のないプレイヤー用に残っているが、現 UI は使用しない）
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

### PythonSubprocessAI
- `FindPythonExecutable()`: Windows では `py` → `python3` → `python` の順で自動検索
- Python ファイルは各 `.csproj` の Content ビルドアクションで出力ディレクトリ（`Othello.Python/`）にコピーされる（`test_*.py` は除外）
- `BeginErrorReadLine()` で stderr を非同期ドレイン（`_stderrBuffer` に蓄積）。同期 `ReadToEnd` によるデッドロックを回避する
- `ReadLineWithTimeout()`: `ReadLineAsync().Wait(timeout)` 方式。専用スレッドを生成しない

### Python AI
- `has_any_flip(board, r, c, player)`: 挟める方向が 1 つでも見つかれば即 `True` を返す短絡評価。有効手一覧生成の高速化に使う
- `count_valid_moves(board, player)`: 座標リストを構築せず件数のみ返す。`evaluate()` の Mobility 計算に使う
- `evaluate_final(board, player, depth=0)`: 勝利 `+(10000+depth)`・敗北 `-(10000+depth)`。残り探索深さを加味して早い勝ちを選好・負けを先送り
- `PlayerColor` の int 値（`Empty=0, Black=1, White=2`）は Python JSON の `"player"` フィールドと直接対応しており変更不可

### テスト
- `Othello.Core.csproj` に `InternalsVisibleTo("Othello.Tests")` を設定済み。`GameEngine.LoadStateForTest(board, player)` で任意の境界局面を再現できる
- `Othello.Tests.csproj` は ViewModels 共有プロジェクト（`.projitems`）を直接取り込んでいるため `GameViewModel` をテスト対象にできる
- **単体テスト**: 変更した関数・クラスの正常系・異常系・境界値を網羅する
- **結合テスト**: 変更が影響する他機能（例: GameEngine の変更なら UI の挙動、AI との連携など）についても結合テストを追加し、機能間の連携が壊れていないことを確認する

