プロジェクトの依存関係に対してサプライチェーンリスク監査を実行します。
NuGet（C#）・cargo（Rust）・pip（Python）の全依存ライブラリを評価します。

---

## Step 1: 依存関係の列挙

### C# / NuGet
```bash
dotnet list package --include-transitive 2>/dev/null
dotnet list package --vulnerable --include-transitive 2>/dev/null
```

### Rust / cargo
```bash
cargo metadata --manifest-path src/Othello.AI/Rust/Cargo.toml --no-deps --format-version 1 2>/dev/null | python -c "import sys,json; pkgs=json.load(sys.stdin)['packages']; [print(p['name'], p['version']) for p in pkgs]"
cargo audit --manifest-path src/Othello.AI/Rust/Cargo.toml 2>/dev/null
```

### Python
```bash
# 標準ライブラリのみ使用（requirements.txt なし）を確認
ls src/Othello.AI/Python/requirements*.txt 2>/dev/null || echo "requirements.txt なし（標準ライブラリのみ）"
```

---

## Step 2: 各依存ライブラリのリスク評価

取得した依存ライブラリそれぞれについて、以下の観点で評価する:

| 評価軸 | 確認方法 | 高リスク条件 |
|---|---|---|
| **CVE 既知脆弱性** | `dotnet list package --vulnerable` / `cargo audit` 結果 | High/Critical 重大度 |
| **メンテナンス状態** | GitHub の最終コミット日・issue 対応状況 | 1年以上更新なし |
| **メンテナー数** | GitHub の contributors 数 | 1人（単一障害点） |
| **ダウンロード数・人気度** | NuGet Gallery / crates.io のダウンロード数 | 極端に少ない（タイポスクワット疑い） |
| **バージョン固定** | `.csproj` / `Cargo.toml` のバージョン指定 | `*` や緩すぎる範囲指定 |
| **推移的依存の深さ** | `--include-transitive` の結果 | 直接使用していない高リスクパッケージ |

---

## Step 3: Othello_CSPY 固有チェック

### PyO3（Rust ↔ Python バインディング）
- `Cargo.toml` の `pyo3` バージョンが最新安定版か確認
- `pyo3` の CVE がないか確認（[crates.io/crates/pyo3](https://crates.io/crates/pyo3) 参照）

### .NET 依存パッケージ
- WPF / WinUI3 は .NET 10 付属のため基本的にリスク低
- `Microsoft.WindowsAppSDK`（WinUI3）のバージョンを確認
- サードパーティ NuGet パッケージがあれば個別評価

### Python（標準ライブラリのみの確認）
- `ai.py` / `alpha_beta.py` が `import` しているモジュールを確認
- 標準ライブラリ以外の `import` がないかチェック:
  ```bash
  grep -rn "^import\|^from" src/Othello.AI/Python/*.py | grep -v "^.*:#" | sort -u
  ```

---

## Step 4: レポート出力

```
## サプライチェーン監査レポート

実行日: YYYY-MM-DD

### 依存関係サマリー
- NuGet パッケージ数: X 件（直接: Y / 推移的: Z）
- cargo クレート数: X 件
- Python 外部依存: なし / X 件

### 🔴 高リスク（即時対応推奨）
- パッケージ名 vX.X.X: 理由（CVE-XXXX-XXXX / 未メンテ / 単一メンテナー）
  → 推奨対応: vY.Y.Y へ更新 / 代替パッケージ検討

### 🟡 中リスク（確認推奨）
- （なければ「なし」）

### 🟢 低リスク・情報
- （なければ「なし」）

### ✅ 問題なし
- （クリーンだった項目）

### 推奨アクション（優先度順）
1. 
```
