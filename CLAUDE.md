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
- `depth`: 探索深さ（Beginner:1, Easy:2, Medium:5, Hard:10, Expert:12）
- `time_ms`: 反復深化の制限時間（ms）。Hard=8000、Expert=15000、それ以外（Beginner/Easy/Medium）は null（固定深さ探索）

**Python → C#（stdout 1行 JSON）**
```json
{"row": 2, "col": 3}
```

Python プロセスのライフサイクル: `StartNewGame()` で起動 → `EndGame()` / `GameViewModel.Dispose()`（ウィンドウ閉鎖時）で停止。

## よくある落とし穴

1. **Board の可変性**: `Board` はミュータブル。履歴（`_history`）や他オブジェクトへ渡す前に必ず `board.Clone()` でコピーする。参照をそのまま保存すると履歴が書き換わり Undo が壊れる
2. **パスロジック**: `AdvanceTurn()` は相手が打てない場合にターンを自動スキップする。一方のプレイヤーをブロックする盤面状態でテストし、スキップが正しく起きることを確認すること
3. **Hard 難易度の計算時間**: 探索深さ 10 の反復深化はスペックの低い環境で遅い場合がある。テストでは `depth=2〜5` で代替するか、`GetBestMoveIterativeDeepening` に十分な時間制限を渡す
4. **座標のインデックス**: `Position(row, col)` は 0〜7 の範囲。UI グリッドのバインディングと行列順が一致していることを確認する
5. **Nullable な勝者**: `GameEngine.GetResult()` が返す `Winner` は `PlayerColor?`（引き分け = `null`）。UI・テスト側で `null` ケースを処理していないとクラッシュする
6. **コメント規約（XML doc）**: `public` / `internal` メンバーには `<summary>` を記載する。テストメソッドの `<summary>` には「何をすると何になるか（パス条件）」を明記する（例: `パス条件: IsSuccess = true かつ GameState が WhiteTurn に変わること`）
7. **無関係な既存バグを独断で処理してしまう**: 作業中に今のタスクと無関係な既存バグを見つけた場合、黙って直す・黙って無視するのではなく、見つけた時点でユーザーに扱い（今の PR に含めるか、別 PR に分けるか）を確認してから進めること
8. **改行コードの混入確認漏れ**: ファイル編集後は `git diff --stat` で差分行数を確認すること。編集ツールが意図せず改行コードを変換すると、実際の変更が数行でもファイル全体が差分になってしまう。`.py` は `.gitattributes`（`*.py text eol=lf`）で LF に固定済みのため対象外。それ以外の拡張子（`.cs`/`.xaml`/`.md` など）を編集した際にこの確認を行う
9. **Python ファイル追加時の確認事項**: 各 `.csproj` は `**\*.py` のワイルドカードで Content ビルドアクション（出力ディレクトリ `Othello.Python/` へのコピー対象）を指定済みのため、新しい `.py` ファイルは個別追加不要で自動的にコピー対象になる。ただし `test_*.py` は `Exclude` 指定で除外されるため、テスト以外の用途のファイルにこの命名を付けないよう注意する

---

## 作業ルール

- **実装前にプランを提示すること**: コード変更を行う前に、方針・変更箇所・影響範囲を日本語でまとめたプランを提示し、ユーザーの承認を得てから実装に進む。
- **承認されたプランは `docs/{機能名}-Plan.md` として書き出す**（`docs/` は `.gitignore` 対象のローカル資料であり PR には含まれない）。実装の経緯を後から追えるようにする
- **複数項目をまとめて依頼された場合は 1 項目ずつサイクルを完結させること**: 「プラン提示 → 承認 → TDD 実装 → ビルド/テスト確認 → コミットメッセージ提示 → 承認 → コミット」のサイクルを 1 項目ごとに完了させてから次の項目に進む。TodoWrite で全項目の進捗を管理し、項目間で状態を見失わないようにする
- **軽微な修正は直接編集可**: 以下の軽微な修正はプラン提示なしで直接編集してよい。ただし変更内容が明らかな場合のみ:
  - スペルミス・タイポ修正
  - 単純な値の誤り（符号違い `+`/`-`、定数値の訂正など）
  - コメント・ドキュメントの文言修正（既存ドキュメントの改善）
  - IDE 警告削除（未使用 using、未使用フィールドなど）

  ただし、以下のような変更は軽微ではないため、プラン提示後に実装すること: ロジック変更が伴う修正、ファイル追加・削除、テスト追加、依存関係の変更
- **GitHub Flow に従うこと**: コード変更は必ず feature ブランチで行い、`main` に直接コミットしない。

  **ブランチ運用手順（GitHub Flow）**

  1. **ブランチ作成** — 作業開始前に `main` から feature ブランチを切る。ブランチ名は作業内容を端的に示す（例: `feature/iterative-deepening`、`fix/undo-pass-bug`、`docs/ai-readme`）
     ```bash
     git switch main
     git pull
     git switch -c feature/<作業名>
     ```
  2. **コミット** — 小さい単位でこまめにコミットする。コミットメッセージは変更の「なぜ」を説明する
  3. **プルリクエスト** — 実装が完了したら `main` への PR を作成する。PR タイトルは 70 文字以内、本文に変更内容・テスト方法を記載する
  4. **マージ後の後始末** — マージ後はローカル・リモートともにブランチを削除する
     ```bash
     git switch main && git pull
     git branch -d feature/<作業名>
     ```

  **禁止事項**
  - `main` ブランチへの直接コミット（緊急の typo 修正など極めて軽微なものを除く）
  - `git push --force` を `main` ブランチに対して実行すること
  - **ユーザーの明示的な指示なしにコミットを実行すること** — 実装完了後はコミットメッセージ案を提示してユーザーの承認を得てからコミットする。「コミットしてください」「commit して」などの明示的な指示があった場合のみ実行する

  **コミットメッセージの書き方**
  - コミットメッセージは**日本語**で記述する
  - 型プレフィックス（`feat`/`fix`/`docs`/`refactor`/`test` など）は英語のままでよい
  - **メッセージは「何をした」よりも「なぜそうしたか」を説明する** — 変更の意図・理由・背景を含める
  - 例:
    - ✓ `fix: Undo 後に AI ターンが残る不具合を修正（スナップショット復元漏れでターンがずれるため）`
    - ✗ `fix: GameEngine.Undo() を修正`（何をしたかだけで理由がない）
  - 例: `feat: Hard 難易度に反復深化を追加`、`docs: AI 層の README を追加`

- **テスト駆動開発を意識すること**: 新機能追加・バグ修正の際は、Red → Green → Refactor のサイクルを厳守する。

  **Red → Green → Refactor サイクル**

  1. **TODO リスト作成（実装前）** — 実装に入る前に、何をテストするかをリストアップする。コーディング中は視野が狭くなるため、冷静な状態で考える。
  2. **テスト記述** — 1 サイクルで倒すテストは必ず 1 つ。複数テストを同時に通そうとしない。
  3. **Red 確認（必須）** — テストを実行して失敗することを確認する。「期待したエラーメッセージ」であることまで検証してから次に進む。この確認を怠るとバグ原因の特定が困難になる。
  4. **Green（最短経路で合格）** — まず動くコードを書く。一時的な汚さは許容し、リファクタリングは次のフェーズに回す。
  5. **Refactor（外部ふるまいを変えず内部品質を上げる）** — テストが Green の状態を保ちながら実施する。こまめにテストを実行し、Red になったら直前の変更を見直す。時間制限を設けて終わりを決める。
  6. **繰り返し** — TODO リストから完了項目を消し、次のテストに進む。

  **アンチパターン（やってはいけないこと）**
  - 複数テストを同時に倒そうとする
  - Red 確認をスキップして実装に入る
  - Refactor 中にテスト実行を怠る
  - Refactor に時間制限を設けず終わりのないリファクタリングを続ける

- **作業完了時の要件**: 作業を終了する際は、以下の状態で完了すること。これらの要件を満たさずに作業を終了しない
  1. **ビルドエラーがないこと** — 変更した範囲の `dotnet build` が成功すること
  2. **警告がないこと** — 未使用 using・未使用フィールドなど、IDE/コンパイラの警告が出ていないこと
  3. **テストが全てパスしていること** — `dotnet test` で全テストが成功すること。AI 層に触れた場合は `test_parity.py`（Rust/Python 一致検証）も実行すること
  4. **コミット・プッシュが完了していること** — ただし**ユーザーの明示的な指示がない限りコミットは実行しない**。コミット可能な状態まで整えたうえでコミットメッセージ案を提示する

## 主要な設計ポイント

### AI 三層構成（C# → Python → Rust）
- `PythonSubprocessAI`（C#）が Python プロセス（`ai.py`）を起動し JSON で通信
- `ai.py` は `alpha_beta.py` 経由でバックエンドを選択する（**シム方式**）
- `alpha_beta.py`: `othello_ai_rust` が import できれば Rust、できなければ `alpha_beta_py.AlphaBetaAI`（純 Python）を使用
- **parity 保証**: `test_parity.py` が Rust 版と Python 版の同一局面・深さでの着手一致を検証する
- Hard/Expert 難易度では `time_ms`（Hard=8000、Expert=15000）を IPC で送信し、反復深化（Iterative Deepening）を使用

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
