Semgrep を使用して Python・C# コードのセキュリティ脆弱性をスキャンします。
Semgrep が未インストールの場合は、パターンマッチによる手動スキャンにフォールバックします。

---

## Step 1: Semgrep の有無を確認

```bash
semgrep --version 2>/dev/null || echo "SEMGREP_NOT_FOUND"
```

---

## Step 2a: Semgrep がある場合（自動スキャン）

### Python AI コードのスキャン
```bash
semgrep scan \
  --config "p/python" \
  --config "p/owasp-top-ten" \
  --config "p/security-audit" \
  src/Othello.AI/Python/ \
  --sarif --output semgrep-python.sarif \
  2>/dev/null
```

### C# コードのスキャン
```bash
semgrep scan \
  --config "p/csharp" \
  --config "p/security-audit" \
  src/ \
  --include="*.cs" \
  --sarif --output semgrep-csharp.sarif \
  2>/dev/null
```

SARIF 結果を読み込んで、severity ごとに整理してレポートを作成する。

---

## Step 2b: Semgrep がない場合（パターンマッチによる手動スキャン）

以下のパターンを Grep で検索する。

### Python — 高リスクパターン

| パターン | リスク種別 |
|---|---|
| `eval(` | コードインジェクション |
| `exec(` | コードインジェクション |
| `subprocess.*shell=True` | コマンドインジェクション |
| `os\.system(` | コマンドインジェクション |
| `pickle\.loads(` | 安全でないデシリアライズ |
| `__import__\(` | 動的インポート |
| `yaml\.load\(` | 安全でないデシリアライズ（`yaml.safe_load` を使うべき） |

対象: `src/Othello.AI/Python/*.py`

### Python — IPC バリデーション確認

`src/Othello.AI/Python/ai.py` で以下を確認:
- `json.loads()` が `try-except` で保護されているか
- `req['board']` 等のフィールドアクセスが例外安全か
- `int(req['player'])` 等の型変換が行われているか

### C# — 高リスクパターン

| パターン | リスク種別 |
|---|---|
| `Process\.Start\(.*\+` | コマンドインジェクション（文字列連結） |
| `shell=true` (大文字小文字不問) | コマンドインジェクション |
| `JsonDocument\.Parse` | try-catch 保護の有無を確認 |
| `Environment\.GetEnvironmentVariable` | 環境変数の信頼性確認 |
| `File\.ReadAll\|File\.WriteAll` | パストラバーサルの可能性 |

対象: `src/**/*.cs`

### Rust — 高リスクパターン

| パターン | リスク種別 |
|---|---|
| `unsafe {` | 安全でないメモリ操作 |
| `\.unwrap()` | パニックによる異常終了 |
| `std::process::Command.*arg.*\+` | コマンドインジェクション |

対象: `src/Othello.AI/Rust/src/**/*.rs`

---

## Step 3: レポート出力

```
## Semgrep スキャンレポート

実行日: YYYY-MM-DD
スキャン方法: Semgrep vX.X.X / パターンマッチ（手動）

### 対象ファイル
- Python: src/Othello.AI/Python/*.py（X ファイル）
- C#: src/**/*.cs（X ファイル）
- Rust: src/Othello.AI/Rust/src/**/*.rs（X ファイル）

### 🔴 高リスク（要対応）
- ファイル:行番号 — パターン — 説明

### 🟡 中リスク（確認推奨）
- （なければ「なし」）

### 🟢 低リスク・情報
- （なければ「なし」）

### ✅ 問題なし
- （クリーンだった項目）

### 推奨アクション（優先度順）
1. 
```

---

## 参考: Semgrep のインストール方法

```bash
# pip 経由（Python 3.8+ 必要）
pip install semgrep

# winget 経由（Windows）
winget install Semgrep.Semgrep
```
