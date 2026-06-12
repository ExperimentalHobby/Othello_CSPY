本プロジェクトの開発履歴から抽出した観点でコードレビューを実施します。
引数としてレビュー対象のファイルパスまたは `git diff` 範囲を指定できます。
省略した場合は `git diff HEAD~1..HEAD`（最新コミット）を対象にします。

---

## Step 0: 対象コードの取得

```bash
# 引数がある場合
git diff $ARGUMENTS 2>/dev/null

# 引数がない場合
git diff HEAD~1..HEAD
```

差分が空の場合は引数に指定されたファイルを直接読み込む。

---

## チェック項目

### 1. リソース管理（過去の実績: Python プロセスリーク修正）

**C# — Process / IDisposable**
- `Process` を保持するクラスは `IDisposable` を実装し、`Dispose()` で `process.Kill()` → `process.Dispose()` を呼んでいるか
- `PythonSubprocessAI.Dispose()` の `_disposed` フラグで二重 Dispose を防いでいるか
- `GameViewModel.Dispose()` が `(_ai as IDisposable)?.Dispose()` で AI を解放しているか
- `using` ブロックまたは `try-finally` で確実に解放されているか

**C# — CancellationTokenSource**
- `_cts.Cancel()` → `_cts.Dispose()` → `_cts = new CancellationTokenSource()` の順で更新しているか
- `OperationCanceledException` を握り潰さず、新規ゲーム開始による正常キャンセルとして扱っているか

---

### 2. 非同期処理（過去の実績: async void → async Task 化、ReadLine デッドロック修正）

**async void の禁止**
- イベントハンドラ以外に `async void` が残っていないか（例外が握り潰される）
- `async Task` + `CancellationToken` に置き換えているか

**ブロッキング I/O の禁止**
- `Process.StandardError.ReadToEnd()` のような同期 ReadToEnd を使っていないか（デッドロックの原因）
- stderr は `BeginErrorReadLine()` で非同期ドレインしているか
- stdout の読み取りは `ReadLineAsync().Wait(timeout)` 方式にしているか

**CancellationToken の競合防止**
- `ProcessAIMoveAsync` / `CheckAndProcessNextTurnAsync` の `catch` 冒頭で `ct.IsCancellationRequested` を確認し早期 return しているか
- 新規ゲーム開始時の旧タスクが新ゲームの状態を破壊しないか

---

### 3. IPC 通信・入力バリデーション（過去の実績: JSON フィールドチェック追加）

**Python — ai.py の入力検証**
- `json.loads()` が `try-except json.JSONDecodeError` で保護されているか
- `board` / `player` / `depth` の必須フィールド存在チェックがあるか
- `req.get('time_ms')` のように省略可能フィールドは `.get()` を使っているか
- `player` 値が `1` か `2` の範囲外だったとき `opponent()` でエラーにならないか（不正値チェック）

**C# — PythonSubprocessAI の送受信**
- `JsonDocument.Parse()` が `try-catch` で保護されているか
- `ReadLineWithTimeout()` でタイムアウト定数（`AiResponseTimeoutMs`）を使っているか（マジックナンバーでないか）
- IPC プロトコル変更（フィールド追加など）がある場合、C# 送信側と Python 受信側の両方に反映されているか

---

### 4. パフォーマンス（過去の実績: HashSet 化、SolidColorBrush キャッシュ、早期 return）

**C# — ViewModel / UI**
- 有効手の含有チェックに `List.Contains()` ではなく `HashSet` を使っているか
- `BoardSquareViewModel` で `new SolidColorBrush()` を毎回生成せず、静的キャッシュを使っているか
- `RefreshBoardDisplay()` 内で不必要な UI 更新が発生していないか（人間のターン時のみハイライトを計算するなど）

**Python — 探索最適化**
- `_alpha_beta()` の冒頭で `depth == 0` の早期 return があるか（リーフノードで不要な手生成をしていないか）
- 有効手の存在確認に `has_any_flip()` の短絡評価を使っているか（全方向をスキャンせず 1 方向見つかれば即 `True`）
- Mobility 計算に `count_valid_moves()`（件数のみ）を使っているか（`get_valid_moves()` でリストを構築していないか）
- ムーブオーダリング（位置重みによる事前ソート）が最大化・最小化の両ノードで適用されているか

**共通**
- `evaluate_final()` が勝利スコアに `depth` を加算して早い勝ちを選好しているか（`±(10000 + depth)`）

---

### 5. ゲームロジックの正確性（過去の実績: Undo スナップショット化、パス自動化）

**Undo の正確性**
- Undo がスナップショット方式（`Board` / `PlayerColor` / `GameState` を丸ごと復元）になっているか
- `_currentPlayer = _currentPlayer.Opponent()` のような単純な反転をしていないか（パスをまたぐ Undo で手番が狂う）
- `OnUndo()` で Undo 後が AI ターンになった場合、さらに 1 回 Undo して人間のターンに戻しているか

**パス処理の集約**
- パスの判定・スキップが `GameEngine.AdvanceTurn()` に集約されているか
- ViewModel / UI 側でパスを手動判定・実行していないか
- `LastPassedPlayer` を参照してメッセージ表示しているか（`NotifyIfPassed()`）

**初期状態の判定**
- 難易度・担当色の変更ガード（`IsInitialState` チェック）が実装されているか
- 最初の一手が置かれた後に難易度・担当色が変更できてしまうソフトロックがないか

---

### 6. 設計・テスタビリティ（過去の実績: AI ファクトリ注入、ConsoleInputParser 分離）

**依存性注入**
- `GameViewModel` コンストラクタが `Func<DifficultyLevel, IAIStrategy>? aiFactory` を受け取っているか
- テストで `FakeAI` を差し込めるか（`null` 時は `CreateDefaultAI` にフォールバックする設計か）
- `CreateDefaultAI` が Python 起動失敗時に `AlphaBetaAI`（C#）へ自動フォールバックしているか

**テスト可能な分離**
- コンソール入力など、テストしづらいロジックが `internal static` クラスに分離されているか
- `InternalsVisibleTo("Othello.Tests")` を利用してテストからアクセスできるか
- `GameEngine.LoadStateForTest()` で任意の境界局面を再現できるか

**定数化**
- タイムアウト値（`AiResponseTimeoutMs`、`PythonProbeTimeoutMs`）、遅延時間（`AiMoveDelayMs`、`AiTurnDelayMs`）、アニメーション時間（`FlipAnimationDurationMs`）がマジックナンバーでなく定数か
- `BOARD_SIZE`（Python）が複数ファイルにハードコードされず一元管理されているか
- AI スクリプトのパスが `AiScriptPaths.AiScriptPath` で一元管理されているか

---

### 7. XAML / UI バインディング（過去の実績: 型不一致修正、未使用リソース削除）

- `IsHitTestVisible` など `bool` 型プロパティに `BoolToVisibilityConverter` を誤用していないか（`InverseBooleanConverter` が必要なケース）
- 未使用の `Style` / `DataTemplate` / `ControlTemplate` が XAML に残っていないか
- 色リソースが `AppColors.xaml` のキー経由になっているか（直書きでないか）
- WPF / WinUI3 の両方に変更が反映されているか（ViewModel は共有、View は個別）

---

---

## dotnet/runtime レビュー基準（C# 一般）

> 出典: [dotnet/runtime コードレビュースキル](https://zenn.dev/sator_imaging/articles/628625956abc18)  
> 43,000 件以上のメンテナーレビューコメントから抽出。本プロジェクトに適用可能な項目のみ収録。

### A. 例外・エラーハンドリング

**内部不変条件のアサート**
- 内部専用呼び出しには `ArgumentException` ではなく `Debug.Assert` を使う
- `null` 抑止演算子（`!`）より `Debug.Assert(value != null)` を優先する
- 網羅的な `switch` の `default` には `UnreachableException` を使う（到達不能な証明）

**例外メッセージの品質**
- パラメータ名は `nameof()` で取得する（文字列リテラルでない）
- 空の例外メッセージを投げない
- ユーザーが行動できる情報（不正値・想定範囲）を含める

**検証順序の統一**
- 1. `ArgumentException` 系（`null` 先、論理検証後）
- 2. `ObjectDisposedException`
- 3. 操作実行時の例外（`InvalidOperationException` など）
- `ThrowIf` ヘルパーを使う（`ArgumentOutOfRangeException.ThrowIfNegative` など手書き if-then-throw を避ける）

**例外の握りつぶし禁止**
- `catch { continue; }` / `catch { return null; }` は根本原因を隠す
- 想定外例外は伝播させるか fail-fast させる
- `OperationCanceledException` はキャンセルの正常終了として扱い、握り潰さない

**`out` パラメータの初期化**
- 全コードパス（エラーパス含む）で `out` パラメータを初期化する

---

### B. パフォーマンス・割り当て

**クロージャによる割り当て回避**
- ラムダがローカル変数をキャプチャするとヒープにクロージャが生成される
- 状態を引数として受け取る `static` デリゲートへ変換を検討する
- `Task.Run(() => _ai.GetBestMove(...))` など AI 呼び出しのラムダでキャプチャを確認する

**コレクションの事前確保**
- 期待件数が分かるなら `List<T>(capacity)`・`Dictionary<K,V>(capacity)`・`HashSet<T>(capacity)` を使う
- 有効手リスト（最大 60 手）など上限が既知のものは特に対象

**繰り返しアクセサのローカル化**
- 同一プロパティを複数回呼ぶならローカル変数にキャッシュする
- `_engine.CurrentPlayer` / `_engine.Board` を繰り返し参照している箇所を確認

**throw ヘルパーの抽出**
- エラーパスの `throw` ロジックを `[DoesNotReturn]` な static ローカル関数に分離する
- 成功パスを JIT がインラインしやすくなる

**O(n²) パターンの回避**
- ループ内の `List.Contains()` や `RemoveAt()` 繰り返しを避ける（`HashSet` / `RemoveAll` へ）
- `GetValidMoves()` の結果をループ内で繰り返し生成していないか確認

---

### C. コードスタイル

**`var` の使用制限**
- 型が文脈から明白な場合のみ使う（`new List<BoardSquareViewModel>()` → `var` は OK）
- 数値型・戻り値・キャスト結果では明示型を使う

**bool パラメータへの名前付き引数**
- `foo(true, false)` ではなく `foo(isEnabled: true, skipValidation: false)` で意図を明示する

**早期 return でネスト削減**
- `else` ブロックでインデントが深くなる場合、前の条件でガード節（早期 return）にする

**不要なデフォルト初期化を省く**
- CLR はゼロ初期化するため `= false`・`= 0`・`= null` の冗長な初期化を書かない（CA1805）

**`sealed` クラスの `Dispose`**
- `sealed` クラスは仮想メソッドの `Dispose(bool)` オーバーロードが不要なケースが多い
- `GameViewModel`・`PythonSubprocessAI` などは直接 `Dispose()` を実装していれば十分

**パターンマッチングを優先**
- 手動の型チェック（`if (x is T) { var t = (T)x; ... }`）より C# パターンを使う
- `if (x is T t) { ... }` / `switch` 式 / `is`・`or`・`and` パターン

**`#region` の禁止**
- `#region` はすぐ実態と乖離する。ファイル分割または部分クラスで整理する

**定数は PascalCase**
- `const` フィールドは PascalCase（例: `AiResponseTimeoutMs`、`BoardSize`）
- `private const` でも同様

---

### D. テスト

**バグ修正には必ず回帰テスト**
- バグ修正コミットに対応するテストが `[InlineData]` 追加または新規テストメソッドとして含まれているか
- そのテストを revert すると確実に失敗するか

**`[Theory]` + `[InlineData]` を優先**
- 類似ケースを複数 `[Fact]` で書かず `[Theory]` + `[InlineData]` にまとめる

**アサーションは具体的に**
- `Assert.True(result != null)` より `Assert.NotNull(result)` や `Assert.Equal(expected, actual)` を使う
- 返ってきた Position の `Row`・`Col` まで検証する（型だけでなく値を確認）

**エッジケースの網羅**
- 盤面境界（`(0,0)`・`(7,7)`）、全マス同色、着手可能手なし（パス局面）、初期配置をテストする
- `depth=0`・`depth=1` などの最小深さで AI が正常動作するか

**フレークなテストは追加しない**
- `Task.Delay` などタイミング依存でたまに失敗するテストは追加しない
- AI 応答時間に依存したアサートは避ける

---

## レポートフォーマット

```
## コードレビュー結果

対象: <ファイル or git diff 範囲>
レビュー日: YYYY-MM-DD

### 🔴 要修正（バグ・リソースリーク・セキュリティ）
- カテゴリ / ファイル:行番号: 問題の説明
  → 修正案

### 🟡 要改善（パフォーマンス・設計・テスタビリティ）
- カテゴリ / ファイル:行番号: 問題の説明
  → 改善案

### 🟢 軽微（マジックナンバー・コメント・命名）
- カテゴリ / ファイル:行番号: 指摘内容

### ✅ 問題なし
- （クリーンだったカテゴリ）

### テストカバレッジ確認
- 変更した関数のテストが存在するか: 有 / 無 / 部分的
- 推奨追加テスト:
```
