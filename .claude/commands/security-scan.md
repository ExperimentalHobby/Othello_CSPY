本プロジェクト向けのセキュリティスキャンを実行します。
以下の 4 項目を順番に検査し、最後に結果をまとめて報告してください。

---

## 検査 1: Rust CVE チェック（cargo audit）

`src/Othello.AI/Rust/` ディレクトリで `cargo audit` を実行し、
Cargo.lock に記録された依存 Crate に既知の脆弱性（CVE）がないか確認します。

```bash
cargo audit --manifest-path src/Othello.AI/Rust/Cargo.toml
```

`cargo-audit` が未インストールの場合は先にインストールします:
```bash
cargo install cargo-audit
```

**確認ポイント**
- `error[vulnerability]` が出たものは severity・影響範囲・修正バージョンを記録する
- `warning[unmaintained]` は中リスクとして記録する
- クリーンなら「問題なし」と記録する

---

## 検査 2: 秘密情報スキャン（ハードコード検出）

以下のパターンを Grep で検索し、ソースコードやスクリプトに秘密情報が埋め込まれていないか確認します。
検索対象: `src/`, `.vscode/`, `.config/`, `*.ps1`, `*.py`, `*.cs`, `*.json`

**検索パターン（各パターンで実行）**

```
# API キー / トークン
password\s*=\s*["'][^"']{8,}
api[_-]?key\s*=\s*["'][^"']{8,}
token\s*=\s*["'][^"']{16,}
secret\s*=\s*["'][^"']{8,}

# 接続文字列
(connection[_-]?string|connstr)\s*=\s*["'][^"']+["']

# AWS / GCP / Azure 認証情報パターン
AKIA[0-9A-Z]{16}
AIza[0-9A-Za-z_-]{35}

# ハードコードされたパス（ユーザー名が含まれるもの）
[Cc]:\\[Uu]sers\\[^\\"\s]{3,}\\(?!AppData)
/home/[^/"\s]{3,}/
```

**確認ポイント**
- `.env` ファイルや `*.potx` など意図的な除外対象は無視する
- 見つかった場合は ファイル・行番号・内容（マスク済み）を記録する
- 設定ファイルのサンプル値（`"YOUR_KEY_HERE"` など）は除外する

---

## 検査 3: Python AI コードの危険パターン検出

`src/Othello.AI/Python/` 配下の Python コードに、
コードインジェクションや安全でない実行につながる危険なパターンがないか確認します。

**検索対象ファイル**: `src/Othello.AI/Python/*.py`

| パターン | リスク |
|---|---|
| `eval(` | コードインジェクション（高） |
| `exec(` | コードインジェクション（高） |
| `subprocess.*shell=True` | コマンドインジェクション（高） |
| `os\.system(` | コマンドインジェクション（高） |
| `pickle\.loads(` | 安全でないデシリアライズ（高） |
| `__import__\(` | 動的インポート（中） |
| `input(` | 未サニタイズ入力（低 — IPC 文脈では通常問題なし） |

また、IPC 処理（`ai.py`）で `json.loads()` が適切に使用されているか確認します:
- `req['board']`, `req['player']`, `req['depth']`, `req.get('time_ms')` の型が検証されているか
- 不正な JSON / 欠損フィールド時に例外が適切に捕捉されているか（`try-except`）

---

## 検査 4: NuGet 依存関係の脆弱性チェック

.NET プロジェクトの NuGet パッケージに既知の脆弱性がないか確認します。

```bash
dotnet list package --vulnerable --include-transitive
```

**確認ポイント**
- `High` / `Critical` 重大度のものを最優先で記録する
- `Moderate` は中リスクとして記録する
- アップデート可能なパッケージには推奨バージョンを記載する
- クリーンなら「問題なし」と記録する

---

## 報告フォーマット

4 つの検査が完了したら、以下の形式でまとめてください。

```
## セキュリティスキャン結果

実行日: YYYY-MM-DD

### 🔴 高リスク（要対応）
- （なければ「なし」と記載）

### 🟡 中リスク（確認推奨）
- （なければ「なし」と記載）

### 🟢 低リスク・情報
- （なければ「なし」と記載）

### ✅ 問題なし
- （クリーンだった検査項目）

### 推奨アクション
1. （対応が必要なものを優先度順に列挙）
```

発見件数が 5 件以上あった場合は、`security-scan-report-YYYY-MM-DD.md` として
`docs/` ディレクトリに保存するか確認してください。
