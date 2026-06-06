# CLAUDE.md

このファイルは、本リポジトリで作業する Claude Code (claude.ai/code) へのガイダンスを提供します。

## プロジェクト概要

**人間 vs Python AI** のオセロ（リバーシ）ゲーム。

- **AI**: Python（alpha-beta 探索）← stdin/stdout JSON で C# と通信
- **UI**: C# .NET 10 + WPF（MVVM パターン）
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

## テスト設計のポイント

- **単体テスト**: 変更した関数・クラスの正常系・異常系・境界値を網羅する。
- **結合テスト**: 変更が影響する他機能（例: GameEngine の変更なら UI の挙動、AI との連携など）についても結合テストを追加し、機能間の連携が壊れていないことを確認する。

## 主要な設計ポイント

- `PythonSubprocessAI.FindPythonExecutable()`: Windows では `py` → `python3` → `python` の順で自動検索
- Python ファイルは `Othello.WPF.csproj` / `Othello.Console.csproj` の Content ビルドアクションで出力ディレクトリ（`Othello.Python/`）にコピーされる（`test_*.py` は除外）
- `HumanColorIndex` / `DifficultyIndex`: ComboBox と ViewModel を繋ぐ int ヘルパープロパティ
- `OnUndo()`: AI ターンになった場合はさらに1回 Undo して人間のターンに戻す
- 有効手ハイライト: 人間のターン時のみ黄色でハイライト（`RefreshBoardDisplay` 内）
- `PlayerColor` の int 値（`Empty=0, Black=1, White=2`）は Python JSON の `"player"` フィールドと直接対応しており変更不可
- `GameEngine.Pass()` は有効手がない場合のみ呼べる。有効手があると `InvalidOperationException` を投げる

