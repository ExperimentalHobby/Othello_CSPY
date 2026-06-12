直近のコード変更をセキュリティ観点でレビューします。
引数として `git diff` の範囲を指定できます（例: `main..HEAD`、`HEAD~1`）。
省略した場合は `HEAD~1..HEAD`（最新コミット）を対象にします。

---

## Phase 0: 差分取得

```bash
git diff $ARGUMENTS 2>/dev/null || git diff HEAD~1..HEAD
```

差分が取得できない場合は `git status` と `git log --oneline -5` で現在の状態を確認する。

---

## Phase 1: 変更の分類

差分を読み、以下のカテゴリに分類する:

| カテゴリ | 確認ポイント |
|---|---|
| **IPC / 通信** | C# ↔ Python の JSON プロトコル変更。`board`, `player`, `depth`, `time_ms` フィールドの型・バリデーション |
| **AI ロジック** | `alpha_beta.py` / `AlphaBetaAI.cs` の探索ロジック変更。境界値・オーバーフロー・無限ループリスク |
| **入力処理** | `json.loads()` / `JsonDocument.Parse()` の例外処理。型チェックの欠落 |
| **プロセス管理** | `subprocess` / `Process` の起動・終了処理。リソースリーク・デッドロックリスク |
| **依存関係** | `Cargo.toml` / `*.csproj` / `requirements.txt` の変更。新規依存の追加 |
| **テスト** | 変更に対応するテストが追加・更新されているか |

---

## Phase 2: セキュリティチェック項目

### Python AI コード変更の場合
- `eval()` / `exec()` / `os.system()` / `subprocess(..., shell=True)` の新規追加がないか
- `json.loads()` の前後に `try-except json.JSONDecodeError` があるか
- `req['board']` 等のキーアクセスが `KeyError` に対して安全か（`.get()` 推奨）
- `pickle.loads()` の使用がないか

### C# コード変更の場合
- `Process.Start()` のコマンドがハードコードされているか（インジェクション防止）
- `JsonDocument.Parse()` / `JsonSerializer.Deserialize()` が try-catch で保護されているか
- `CancellationToken` の伝搬が適切か
- IDisposable リソースが `using` または `Dispose()` で解放されているか

### Rust コード変更の場合
- `unsafe` ブロックの新規追加がないか
- PyO3 の境界値チェックが適切か
- `unwrap()` / `expect()` が本番パスに残っていないか（`?` 演算子を推奨）

### 全言語共通
- ハードコードされたパス（`C:\Users\...`）や認証情報がないか
- テストカバレッジ: 変更した関数に対するテストが存在するか

---

## Phase 3: 影響範囲の評価

変更が以下に波及していないか確認する:

1. **IPC プロトコル変更** → `ai.py` の入力パース / `PythonSubprocessAI` の送受信双方に影響
2. **GameEngine 変更** → ViewModel の動作 / Undo スナップショットの整合性
3. **Evaluator 変更** → AI の着手品質 / `test_parity.py` の通過可否
4. **依存関係追加** → サプライチェーンリスク（`/supply-chain-audit` で追加確認を推奨）

---

## Phase 4: レポート出力

以下の形式でまとめる:

```
## Differential Review レポート

対象範囲: <git diff 範囲>
レビュー日: YYYY-MM-DD

### 🔴 要対応（高リスク）
- （なければ「なし」）

### 🟡 要確認（中リスク）
- （なければ「なし」）

### 🟢 情報・改善提案
- （なければ「なし」）

### ✅ 問題なし
- （クリーンだった項目）

### テストカバレッジ
- 変更関数に対するテスト: 有 / 無 / 部分的
- 推奨追加テスト: （あれば記載）

### 推奨アクション（優先度順）
1. 
```
