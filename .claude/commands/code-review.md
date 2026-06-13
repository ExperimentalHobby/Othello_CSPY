本プロジェクトの開発履歴から抽出した観点でコードレビューを実施します。
引数としてレビュー対象のファイルパスまたは `git diff` 範囲を指定できます。
省略した場合は `git diff HEAD~1..HEAD`（最新コミット）を対象にします。

---

## レビュワーの心構え

丁寧に、しかし徹底的に懐疑的であること。目標はメンテナーのレビュー速度を上げることであり、  
PR 著者が見落とした問題を見つけるだけでなく、変更全体の価値を問い直すことも含む。  
PR 説明や関連 Issue の記述は「検証すべき主張」であり、受け入れるべき事実ではない。  
方向性に疑問を感じたら遠慮なく指摘し、不確かな点でも懸念を表明することを躊躇わない。

---

## Step 0: 対象コードの取得

```bash
# 引数がある場合
git diff $ARGUMENTS 2>/dev/null

# 引数がない場合
git diff HEAD~1..HEAD
```

差分が空の場合は引数に指定されたファイルを直接読み込む。

**取得すべきコンテキスト（差分だけでなく以下も収集）：**
1. **変更ファイルの全文を読む** — 差分ハンクだけでなく、変更された各ファイルの全体を読む。  
   差分のみのレビューは偽陽性と見落としの最大要因。不変条件・ロックプロトコル・データフローは前後のコードを見なければ分からない。
2. **呼び出し元を Grep で確認** — 変更した API・メソッドが public/internal の場合、呼び出し元を検索し、変更が呼び出し元の前提を破っていないか確認する。
3. **関連型のチェック** — 同一インターフェースの別実装（例: `IAIStrategy` の他の実装）に同じ問題がないか確認する。
4. **git 履歴の確認** — `git log --oneline -20 -- <file>` で最近の変更を確認。リバートの有無・同じ問題への過去の修正試行を見る。

---

## ホリスティック評価（コード詳細を見る前に）

変更全体を俯瞰し、以下の問いに答えること。個別行レビューより先に評価する。

### 動機・必要性
- **「これは本当に必要か？」** — 新コード・新 API・新フラグは存在を正当化しなければならない。回避できるなら回避すべき。
- **根本原因を修正しているか？** — 症状への対処や警告の抑制ではなく、根本原因を修正しているか。
- **複雑なアプローチへの対抗質問**「なぜ単純に X しないのか？」— より単純な代替案がないか常に問う。

### スコープ・集中度
- **PR は単一の関心事に集中しているか？** — 複数の懸念が混在していないか。レビューが難しくなり回帰リスクが上がる。
- **本 PR の目的でない改善は follow-up に回す。**

### リスク・互換性
- **振る舞いの変更は下流コンポーネントに影響しないか？** — C# ↔ Python IPC プロトコル変更・GameEngine 変更・ViewModel 変更などの影響範囲を確認する。

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

### 3. スレッドセーフティ（過去の実績: WinUI3 COM スレッド違反修正）

- **クロススレッドフィールドアクセスは `Volatile` または `Interlocked` を使う** — あるスレッドで書かれ別スレッドで読まれるフィールドは `Volatile.Read/Write` または `Interlocked` を使う
- **`??=` 演算子はスレッドセーフでない** — 読み取り→書き込みの間にレースが発生しうる
- **UI バインディングプロパティは UI スレッドからのみ更新する** — `Task.Run()` 内部で `PropertyChanged` を発火させるとスレッド違反。`await Task.Run(...)` 後の継続（UI スレッド）で更新する
- WinUI3 では `DispatcherQueue.TryEnqueue()` または `await` 後継続でのみ UI プロパティを更新する
- `IsBeingFlipped`・`AiEngineLabel` など UI バインディングプロパティが `Task.Run()` 内から更新されていないか

---

### 4. IPC 通信・入力バリデーション（過去の実績: JSON フィールドチェック追加）

**Python — ai.py の入力検証**
- `json.loads()` が `try-except json.JSONDecodeError` で保護されているか
- `board` / `player` / `depth` の必須フィールド存在チェックがあるか
- `req.get('time_ms')` のように省略可能フィールドは `.get()` を使っているか
- `player` 値が `1` か `2` の範囲外だったとき `opponent()` でエラーにならないか（不正値チェック）

**C# — PythonSubprocessAI の送受信**
- `JsonDocument.Parse()` が `try-catch` で保護されているか
- `ReadLineWithTimeout()` でタイムアウト定数（`AiResponseTimeoutMs`）を使っているか（マジックナンバーでないか）
- IPC プロトコル変更（フィールド追加など）がある場合、C# 送信側と Python 受信側の両方に反映されているか

**Python — トランスポジションテーブルの境界値誤用**
- Python 側の TT エントリが `(score, depth)` のみで NodeType（Exact / LowerBound / UpperBound）を管理していない場合、βカットで得た下界値や fail-low で得た上界値を正確値として再利用するリスクがある
- C# `AlphaBetaAI` はすでに `NodeType` を導入済み。Python 側に同様の対処がないなら、**異なる探索窓でヒットした場合に誤った評価値が返る可能性がある**ことを指摘する
- 最小修正は「`alpha < cached_score < beta` のときのみ TT ヒットとして返す」こと

---

### 5. パフォーマンス（過去の実績: HashSet 化、SolidColorBrush キャッシュ、早期 return）

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

**Python — Move Ordering のコストトレードオフ**
- ムーブオーダリングのソートキーが `WEIGHTS[m[0]][m[1]]`（着手先マスの位置重みのみ・O(1)）か、`evaluate_positional(make_move(board, ...))`（着手後の盤面全体を評価・O(n) per 候補手）かを確認する
- 後者は精度が高い（着手によって盤面全体の評価が変わることを考慮）が、各ノードで全候補手分の `make_move` が発生するため探索コストが増大する。現在のリポジトリは前者（高速・シンプル）を採用
- 変更する場合は `test_parity.py` で Rust/Python の着手一致が維持されることを確認すること

---

### 6. ゲームロジックの正確性（過去の実績: Undo スナップショット化、パス自動化）

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

### 7. 設計・テスタビリティ（過去の実績: AI ファクトリ注入、ConsoleInputParser 分離）

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

### 8. XAML / UI バインディング（過去の実績: 型不一致修正、未使用リソース削除）

- `IsHitTestVisible` など `bool` 型プロパティに `BoolToVisibilityConverter` を誤用していないか（`InverseBooleanConverter` が必要なケース）
- 未使用の `Style` / `DataTemplate` / `ControlTemplate` が XAML に残っていないか
- 色リソースが `AppColors.xaml` のキー経由になっているか（直書きでないか）
- WPF / WinUI3 の両方に変更が反映されているか（ViewModel は共有、View は個別）

**`IValueConverter.Convert()` の例外処理**
- `catch { }` (bare catch) でバインディングエラーをサイレントに飲み込んでいないか — デバッグ時に問題の原因が特定できなくなる
- 想定できる例外（`FormatException`、`OverflowException` など）のみを具体的に捕捉し、それ以外は伝播させる:
  ```csharp
  // NG: FormatException 以外のバグも隠す
  catch { return new SolidColorBrush(Colors.Transparent); }

  // OK: 想定できる parse エラーのみ握る
  catch (FormatException) { return new SolidColorBrush(Colors.Transparent); }
  ```
- WinUI3 固有: `StringToBrushConverter` で毎回 `new SolidColorBrush()` を生成していないか — 既存の静的キャッシュ（`AppColors.xaml` 等）を経由できないか確認する

---

## dotnet/runtime レビュー基準（C# 一般）

> 出典: dotnet/runtime `.github/skills/code-review/SKILL.md`（43,000 件以上のメンテナーレビューコメントから抽出）  
> 本プロジェクトに適用可能な項目のみ収録。JIT・GC・ネイティブ相互運用など本プロジェクト非適用の項目は除外。

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
- **例外の握りつぶしに対して積極的に質問する** — try/catch でサイレントに捨てる場合、その例外が本当に「予期できる・回復可能」な状態なのかを問う。そうでなければデバッグを困難にする根本原因隠蔽になる。

**`catch { }` (bare catch / 型なし catch) の禁止**
- 型を指定しない `catch { }` は `OperationCanceledException` を含む全例外を飲み込む — キャンセルが上位に伝播しなくなる
- フォールバック目的でも bare catch は使わない。代わりに `when` ガードで OCE を透過させる:
  ```csharp
  // NG: キャンセルも飲み込む
  try { return await _primary.GetAsync(...); }
  catch { /* fallback */ }

  // OK: OCE は透過、それ以外のエラーでフォールバック
  try { return await _primary.GetAsync(...); }
  catch (Exception ex) when (ex is not OperationCanceledException)
  { /* fallback */ }
  ```
- `catch (Exception)` も同様 — `when (ex is not OperationCanceledException)` なしで使っていないか確認する
- **フォールバックパターン（Primary → Secondary）固有チェック**:
  1. Primary の `catch` が OCE を透過させているか
  2. Secondary も同じ `CancellationToken` を受け取っているか
  3. Secondary の例外も適切に処理されているか（サイレント無限フォールバックになっていないか）

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

**テストヘルパークラスを活用する**
- `src/Othello.Tests/Helpers/` の `TestBoardHelper` / `TestGameHelper` / `TestPositionHelper` を使い、テストごとに同じ盤面生成コードを繰り返し書いていないか確認する
- `TestBoardHelper.CreateBoardWithPieces(...)` — 境界盤面のセットアップ
- `TestGameHelper.CreateGameWithMoves(...)` — 特定局面まで進めたゲーム状態の生成
- `TestPositionHelper.GetOpeningMovesForBlack()` — 初期盤面の黒の有効手を正しく列挙しているかの検証

**フレークなテストは追加しない**
- `Task.Delay` などタイミング依存でたまに失敗するテストは追加しない
- AI 応答時間に依存したアサートは避ける

**非同期完了の待機**
- `async` を呼び出すコードのテストでは `SpinWait.SpinUntil` + `Volatile.Read` で完了を待つ
- `Task.Delay` による固定スリープは使わない（タイミング依存になる）

---

### E. コメント・ドキュメント

- **コメントは「なぜ（Why）」を説明し「何を（What）」を繰り返さない** — コードを英語に直訳したコメントは削除すべき
- **コードを変更したら陳腐化したコメントを削除・更新する** — 古い挙動を説明するコメントはないより害がある
- **TODO は GitHub Issue 番号を参照する** — `// TODO: #123 ～` の形式で追跡可能にする。Issue 番号なしの TODO は対応されない可能性が高い

---

## 偽陽性を避ける

- **指摘前に差分だけでなく全コンテキストで懸念が本当に成立するか確認する** — 呼び出し元・呼び出し先・ラッパー層で既に対処されていないか検証する
- **現実的でない理論上の懸念はスキップする** — 「起こりえる」は「起こる」ではない
- **確信が持てない場合は断定より質問の形で表明する** — 「この場合 X になりませんか？」
- **訓練データの知識で「存在しない・非推奨・利用不可」と断言しない** — 知識カットオフ以降の変更を確認してから指摘する
- **提案するコードは必ず動作するものにする** — 構文が正しく、呼び出せる API のみを使う

---

## レポートフォーマット

```
## コードレビュー結果

対象: <ファイル or git diff 範囲>
レビュー日: YYYY-MM-DD

### ホリスティック評価
**動機**: <この変更が正当化されるか・問題が実在するか 1〜2 文>
**アプローチ**: <正しいレイヤー・正しい方法で修正しているか 1〜2 文>
**判定**: <✅ LGTM / ⚠️ 要人間レビュー / ⚠️ 要修正 / ❌ 却下> — <2〜3 文で判定根拠>

---

### 詳細指摘

#### ❌/⚠️/💡 <カテゴリ> — <概要>

<具体的な説明。ファイル:行番号・インターリーブ・コード例を含む>

（指摘ごとに繰り返す。同じ問題が複数箇所にある場合は1件にまとめ全箇所を列記する）
```

### 重大度の定義

| 記号 | 意味 | マージブロッカー |
|------|------|----------------|
| ❌ error | バグ・セキュリティ問題・API 違反・振る舞い変更のテスト欠如 | はい |
| ⚠️ warning | パフォーマンス問題・欠損バリデーション・パターン不整合 | 通常はい |
| 💡 suggestion | スタイル改善・可読性向上・任意の最適化 | いいえ |

### 判定一貫性ルール

1. **判定は最も深刻な指摘を反映する** — ⚠️ が 1 件でも「LGTM」は不可。代わりに「要人間レビュー」または「要修正」を使う。
2. **不確かな場合は常に人間レビューにエスカレート** — 確信が持てないとき、判定を「LGTM」にしてはならない。誤った LGTM は不要なエスカレーションより遥かに有害。
3. **コードの正しさとアプローチの完全性を分離する** — コードとして正しくても、根本原因を修正せず症状に対処しているだけの変更は「要修正」になりえる。
4. **⚠️ 指摘それぞれについて「このままマージしてよいか」を確認する** — 1 件でも「否」なら「要修正」。「不明」なら「要人間レビュー」。
