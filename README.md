# Othello (オセロ) — C# .NET 10 × Python AI × Rust

人間 vs CPU（AI）のオセロ（リバーシ）ゲームです。  
ゲームの UI・ロジックは C# (.NET 10)、AI（アルファベータ探索）は **Rust** で実装しています。  
C# ↔ Python は stdin/stdout の JSON 通信で連携し、Python は AI 計算を Rust 拡張（PyO3）へ委譲します（**C# → Python → Rust**）。  
Python が利用できない環境では **C# 純粋実装 AI** に自動フォールバックするため、Python・Rust ともに必須ではありません。

## 機能

### ゲームモード
- **WPF 版**: グラフィカルな盤面でマウス操作（Windows 専用）
- **WinUI3 版**: Windows App SDK を使用したモダン UI（Windows 専用）
- **コンソール版**: ターミナル上でテキスト形式のゲームプレイ（クロスプラットフォーム）

### ゲームプレイ
- 人間の担当色を選択可能（黒＝先手 / 白＝後手）
- 難易度 3 段階（イージー / ノーマル / ハード）
- 有効手のハイライト表示（WPF・WinUI3 版：黄色、コンソール版：`*`）
- 有効手がない場合は自動パス（画面にメッセージ通知）・1手戻す（Undo）機能
- 最初の一手が置かれるまで難易度・担当色を変更可能

### AI（三段階フォールバック）

メニューバー左端のバッジで使用中のバックエンドを確認できます。  
バッジは Python プロセス起動直後のハンドシェイクで確定するため、ファイル存在ではなく **実際の import 結果** を反映します。

| バッジ | バックエンド | 条件 |
|--------|------------|------|
| **AI: Rust** | PyO3 製 Rust 拡張 | Rust ビルド済み & Python あり（import 成功） |
| **AI: Python** | 純 Python 実装 | Python あり（Rust 未ビルド or import 失敗） |
| **AI: C#** | C# 純粋実装 | Python 未インストール |

- αβ探索 + Zobrist トランスポジションテーブル（全バックエンド共通アルゴリズム）
- ムーブオーダリング（位置重みによる事前ソート）で探索効率を向上
- 有効手判定は短絡評価（`HasAnyFlip`）で高速化
- 難易度別探索深さ: イージー=2、ノーマル=5、ハード=10
- **Hard 難易度では全バックエンドで反復深化（時間制限 8 秒）を使用**

## システム要件

| 項目 | WPF 版 / WinUI3 版 | コンソール版 |
|------|-------------------|------------|
| OS | Windows 10/11 | 任意の OS |
| .NET | .NET 10 SDK | .NET 10 SDK |
| Python | 3.8 以上（任意・なければ C# AI で動作） | 同左 |
| Windows App Runtime | WinUI3 版のみ: 2.0 以上 | 不要 |
| Rust ツールチェーン | 任意（AI を Rust で高速化する場合のみ） | 同左 |

> **Windows App Runtime のインストール（WinUI3 版のみ）**
> ```
> winget install Microsoft.WindowsAppRuntime.2.0 --accept-package-agreements --accept-source-agreements
> ```

## 開発環境

### Visual Studio 2026 以降

**インストール時に必要なワークロード（Visual Studio Installer で選択）:**

| ワークロード | 用途 |
|---|---|
| .NET デスクトップ開発 | WPF・WinUI3・コンソール・テストのビルドに必要 |

> .NET 10 SDK が含まれていない場合は「個別のコンポーネント」タブで `.NET 10.0 Runtime` を追加インストールしてください。

### Visual Studio Code

**推奨拡張機能:**

| 拡張機能 | ID | 用途 |
|---|---|---|
| C# Dev Kit | `ms-dotnettools.csdevkit` | C# の開発・デバッグ |
| Python | `ms-python.python` | Python の実行・デバッグ |
| Pylance | `ms-python.vscode-pylance` | Python 言語サーバー（補完・型チェック） |
| Rust Analyzer | `rust-lang.rust-analyzer` | Rust の補完・定義ジャンプ・インレイヒント |
| Even Better TOML | `tamasfe.even-better-toml` | `Cargo.toml` / `pyproject.toml` の構文ハイライト |

### Python

| 項目 | 要件 |
|------|------|
| バージョン | 3.8 以上 |
| パッケージ | 標準ライブラリのみ（追加インストール不要） |
| Windows | Python Launcher（`py`）の使用を推奨 |

> Python が未インストールの場合、AI は自動的に C# 実装（`AlphaBetaAI`）で動作します。

### Rust（任意・AI 高速化）

AI（アルファベータ探索）を高速な Rust 実装に置き換える場合のみ必要です。  
**ビルドしなくても Python または C# 実装で動作するため、この環境構築は任意**です。

| 項目 | 要件 / 導入例 |
|------|------|
| Rust | [rustup](https://rustup.rs/) で stable を導入 |
| maturin | `py -m pip install --user maturin`（PyO3 拡張のビルドツール） |
| C リンカ | Windows: [VS Build Tools](https://visualstudio.microsoft.com/visual-cpp-build-tools/) の「C++ によるデスクトップ開発」ワークロード ／ Linux: `gcc` など |

> **VS Build Tools のインストール（Windows）**
> ```
> winget install Microsoft.VisualStudio.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --quiet" --accept-package-agreements --accept-source-agreements
> ```

---

## セットアップ

### 1. リポジトリのクローン
```bash
git clone <リポジトリURL>
cd Othello_CSPY
```

---

### 2. ビルド

#### コマンドライン（dotnet CLI）

```bash
# 全プロジェクトビルド（WPF / Console / Tests）
dotnet build Othello.slnx

# WinUI3 版ビルド（x64 プラットフォーム指定が必要）
dotnet build Othello.slnx -p:Platform=x64

# WPF 版を起動
dotnet run --project src/Othello.GUI/WPF/Othello.WPF.csproj

# WinUI3 版を起動
dotnet run --project src/Othello.GUI/WinUI3/Othello.WinUI3.csproj

# コンソール版を起動
dotnet run --project src/Othello.Console/Othello.Console.csproj
```

---

#### Visual Studio

1. `Othello.slnx` をダブルクリックして開く
2. ツールバーのプラットフォームドロップダウンで用途に合わせて切り替える
   - **Any CPU**: WPF・Console・Tests をビルド
   - **x64**: WPF・Console・Tests + WinUI3 をビルド
3. 起動するプロジェクトを右クリック → **スタートアッププロジェクトに設定**
4. **デバッグ → デバッグの開始**（`F5`）または **デバッグなしで開始**（`Ctrl+F5`）

> **テスト実行**: メニュー **テスト → すべてのテストを実行**（`Ctrl+R, A`）

---

#### Visual Studio Code

| 操作 | 手順 |
|------|------|
| WPF ビルド（デフォルト） | `Ctrl+Shift+B` |
| WinUI3 ビルド | `Ctrl+Shift+P` → `Tasks: Run Task` → `build: WinUI3` |
| AI 拡張（Rust）をビルド | `Ctrl+Shift+P` → `Tasks: Run Task` → `build: Rust AI` |
| テスト実行（.NET） | `Ctrl+Shift+P` → `Tasks: Run Test Task` |
| カバレッジ計測 + レポート生成 | `Ctrl+Shift+P` → `Tasks: Run Task` → `test: Coverage` |
| Rust テスト | `Ctrl+Shift+P` → `Tasks: Run Task` → `test: Rust` |
| Python テスト | `Ctrl+Shift+P` → `Tasks: Run Task` → `test: Python` |
| WPF 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WPF` |
| WinUI3 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WinUI3` |
| コンソール版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: Console` |
| dist へ発行（WPF） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WPF → dist/WPF` |
| dist へ発行（WinUI3） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WinUI3 → dist/WinUI3` |
| dist へ発行（両方） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: all → dist` |

> **カバレッジ計測スクリプト**（`tools/measure-coverage.ps1`）を使うと、テスト実行からレポート生成まで一括で行えます。  
> ```powershell
> .\tools\measure-coverage.ps1
> ```
> レポートは `report/YYYYMMDD-HHMMSS_<ブランチ名>_report/index.html` に生成されます（`report/` は `.gitignore` 対象）。  
> `reportgenerator` は `.config/dotnet-tools.json` のローカルツールとして実行されるため、グローバルインストールは不要です。

> `publish: *` タスクは発行前に自動で `build: Rust AI` を実行し、Rust 製 AI を dist に同梱します。Rust 未導入の場合は先に [Rust（任意・AI 高速化）](#rust任意ai-高速化) を導入してください。

---

### 3. AI 拡張（Rust）のビルド（任意・高速化）

AI のアルファベータ探索を Rust 実装（[src/Othello.AI/Rust/](src/Othello.AI/Rust/)）へ委譲することで高速化できます。  
**ビルドしない場合でも Python または C# 実装で動作する**ため、この手順は任意です。

```powershell
pwsh -File src/Othello.AI/Rust/build_rust.ps1
```

成功すると `othello_ai_rust.pyd`（Windows）/ `othello_ai_rust*.so`（Linux・macOS）が
`src/Othello.AI/Python/` に配置されます。以降の `dotnet build` で出力ディレクトリへ自動コピーされます。

> 生成物（`.pyd`/`.so`）は OS・アーキテクチャ依存のためリポジトリには含めません（`.gitignore` 済み）。

> **Windows の `.pyd` ロック対策**: Python プロセスが `.pyd` を読み込み中にビルドすると `IOException` が発生することがあります。`build_rust.ps1` は最大 5 回（1 秒間隔）リトライするため、通常はそのまま再実行するか、Python プロセスを終了してから再試行してください。

**テスト:**

```powershell
# Rust 単体テスト（Python 非依存）
cargo test --manifest-path src/Othello.AI/Rust/Cargo.toml --no-default-features

# Python テスト（Rust↔Python 整合性テストを含む）
py -m unittest discover -s src/Othello.AI/Python -p "test_*.py"
```

---

## 操作方法

### WPF 版 / WinUI3 版

| 操作 | 内容 |
|------|------|
| 黄色のマスをクリック | 石を置く |
| 新規ゲーム ボタン | ゲームを最初からやり直す |
| 戻す ボタン | 直前の手を 1 手取り消す |
| 難易度 コンボボックス | AI の強さを変更（最初の一手が置かれるまで変更可能） |
| あなたの色 コンボボックス | 担当色を変更（最初の一手が置かれるまで変更可能） |

> **パスについて**: 有効手がない場合はゲームエンジンが自動的にスキップし、画面にメッセージを表示します。手動でのパス操作は不要です。

### コンソール版

起動後、難易度と担当色を選択してゲームが始まります。

```
あなたの手（黒、例: d4）: d3
```

| 入力形式 | 例 | 意味 |
|---------|-----|------|
| 列文字 + 行番号 | `d4` | d 列の 4 行目 |
| 行 スペース 列（0始まり） | `3 3` | 行 3・列 3 |
| `undo` | — | 1 手取り消す |

---

## プロジェクト構成

```
Othello_CSPY/
├── src/
│   ├── Othello.Core/          # ゲームロジック層（.NET 10）
│   │   ├── Models/            # Board, Position, PlayerColor, GameState, MoveResult
│   │   ├── Rules/             # OthelloRules, FlipCalculator
│   │   └── Game/              # GameEngine
│   │
│   ├── Othello.ViewModels/    # 共有 ViewModel 層（.shproj 共有プロジェクト）
│   │   ├── ViewModelBase.cs
│   │   ├── RelayCommand.cs    # #if WPF で CommandManager と手動通知を切り替え
│   │   ├── BoardSquareViewModel.cs
│   │   └── GameViewModel.cs   # AI ファクトリ注入・C# フォールバック・AiEngineLabel
│   │
│   ├── Othello.GUI/           # GUI 層（Windows 専用）
│   │   ├── WPF/               # WPF UI 層（.NET 10）
│   │   │   ├── Converters/    # BoolToVisibilityConverter, PlayerColorToBrushConverter
│   │   │   ├── Resources/     # AppColors.xaml（色リソース）, AppStyles.xaml（スタイル）
│   │   │   ├── App.xaml       # リソースディクショナリのマージ
│   │   │   └── MainWindow.xaml
│   │   └── WinUI3/            # WinUI3 UI 層（.NET 10）
│   │       ├── Converters/    # BoolToVisibilityConverter, InverseBooleanConverter, PlayerColorToBrushConverter
│   │       ├── Program.cs     # Bootstrap.TryInitialize による明示的な Runtime 初期化
│   │       ├── App.xaml
│   │       └── MainWindow.xaml
│   │
│   ├── Othello.Console/       # コンソール版（.NET 10）
│   │   └── Program.cs
│   │
│   ├── Othello.AI/            # AI 層
│   │   ├── Core/              # 共有 AI インターフェース（Othello.Core に取り込み）
│   │   │   ├── IAIStrategy.cs
│   │   │   ├── DifficultyLevel.cs
│   │   │   ├── AiScriptPaths.cs
│   │   │   └── PythonSubprocessAI.cs
│   │   ├── CSharp/            # C# 純粋実装 AI（Python 不在時のフォールバック）
│   │   │   ├── AlphaBetaAI.cs # αβ探索 + Zobrist トランスポジションテーブル
│   │   │   └── Evaluator.cs   # 位置重み・石数・Mobility による評価関数
│   │   ├── Python/            # Python AI（C# との窓口 + フォールバック）
│   │   │   ├── ai.py          # エントリポイント（起動時ハンドシェイク + stdin/stdout ループ）
│   │   │   ├── alpha_beta.py  # バックエンド選択シム（Rust 優先・Python フォールバック）
│   │   │   ├── alpha_beta_py.py # 純 Python αβ探索（フォールバック実装）
│   │   │   ├── evaluator.py   # 盤面評価関数
│   │   │   └── board.py       # 盤面操作ユーティリティ
│   │   └── Rust/              # Rust 製 AI 拡張（PyO3 / abi3）
│   │       ├── src/lib.rs     # αβ探索・評価・盤面操作
│   │       ├── Cargo.toml
│   │       ├── pyproject.toml # maturin ビルド設定
│   │       └── build_rust.ps1 # ビルド & .pyd/.so 配置ヘルパー
│   │
│   └── Othello.Tests/         # xUnit テスト（.NET 10）
│
├── .claude/commands/          # Claude Code スラッシュコマンド
│   ├── debug-ai.md            # /debug-ai           : AI デバッグ手順
│   ├── new-feature.md         # /new-feature        : 新機能追加手順
│   ├── update-style.md        # /update-style       : スタイル変更ガイド
│   ├── code-review.md         # /code-review        : プロジェクト固有コードレビュー
│   ├── security-scan.md       # /security-scan      : セキュリティ4項目スキャン
│   ├── diff-review.md         # /diff-review        : 差分セキュリティレビュー
│   ├── supply-chain-audit.md  # /supply-chain-audit : サプライチェーンリスク評価
│   └── semgrep-scan.md        # /semgrep-scan       : 脆弱性パターンスキャン
├── tools/
│   └── measure-coverage.ps1   # カバレッジ計測スクリプト（report/ に HTML レポートを生成）
├── .config/
│   └── dotnet-tools.json      # reportgenerator（カバレッジ HTML レポート）
├── Othello.slnx               # 統合ソリューション（WPF / WinUI3 / Console / Tests）
├── LICENSE
└── README.md
```

---

## アーキテクチャ

### AI 三段階フォールバック

```
GameViewModel._aiFactory()
        │
        ├─ PythonSubprocessAI（Python あり）
        │       │
        │       └─ alpha_beta.py ─┬─ othello_ai_rust（Rust ビルド済み）→ AI: Rust
        │                         └─ alpha_beta_py（純 Python）         → AI: Python
        │
        └─ AlphaBetaAI（Python なし）                                   → AI: C#
```

### レイヤー構成

```
┌─────────────────────────────────────────────┐
│        UI 層                                 │
│  Othello.GUI/WPF  /  Othello.GUI/WinUI3     │
│  (XAML + コードビハインド + Converters)      │
└──────────────────┬──────────────────────────┘
                   │ DataContext
┌──────────────────▼──────────────────────────┐
│        ViewModel 層（共有）                  │
│  Othello.ViewModels (.shproj)               │
│  GameViewModel / BoardSquareViewModel        │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│        ゲームロジック層                       │
│  Othello.Core                               │
│  GameEngine / OthelloRules / Board          │
└──────────────────┬──────────────────────────┘
                   │ stdin/stdout JSON（Python あり時）
┌──────────────────▼──────────────────────────┐
│        AI 層                                 │
│  PythonSubprocessAI ─ ai.py ─ Rust 拡張    │
│  AlphaBetaAI（C#）← Python 不在時           │
└─────────────────────────────────────────────┘
```

### C# ↔ Python 通信（IPC）

C# から Python プロセスを起動し、stdin/stdout で JSON メッセージを 1 行ずつやり取りします。

```
C# (PythonSubprocessAI)                Python (ai.py)
        |                                     |
        |                     ← stdout: {"backend": "rust"} (起動直後ハンドシェイク)
        |  ← EngineName を確定（AI: Rust / AI: Python）
        |                                     |
        |  (以降、1 手ごとに繰り返す)          |
        |--- stdin: {"board":..., "player":1, "depth":5, "time_ms":null} -->|
        |                                     | Rust 拡張で alpha-beta 探索
        |                                     | （未ビルド時は純 Python）
        |<-- stdout: {"row":2, "col":3} ------|
```

Python プロセスは 1 ゲームにつき 1 つ起動され、ゲーム終了時に停止します。  
起動直後の **ハンドシェイク行**（`{"backend": "rust" | "python"}`）で使用バックエンドを確定します。  
Hard 難易度では `time_ms=8000` を付加し、反復深化（Iterative Deepening）を使用します（C# フォールバック AI も同様）。

### 評価関数の重み付け

```
[100, -20,  10,   5,   5,  10, -20, 100]
[-20, -50,  -2,  -2,  -2,  -2, -50, -20]
[ 10,  -2,   5,   1,   1,   5,  -2,  10]
[  5,  -2,   1,   2,   2,   1,  -2,   5]
[  5,  -2,   1,   2,   2,   1,  -2,   5]
[ 10,  -2,   5,   1,   1,   5,  -2,  10]
[-20, -50,  -2,  -2,  -2,  -2, -50, -20]
[100, -20,  10,   5,   5,  10, -20, 100]
```

- コーナー（+100）: 最優先で取りにいく
- X-square（-50）: コーナー斜め隣はリスクが高いため避ける
- Mobility（+10/手）: 着手の選択肢が多いほど有利（中盤）
- 石数差（×10）: 終盤（50 手以降）に加算

---

## 名前空間

```
Technopro.Othello.Core.Models
Technopro.Othello.Core.Rules
Technopro.Othello.Core.Game
Technopro.Othello.Core.AI      # IAIStrategy, DifficultyLevel, PythonSubprocessAI, AlphaBetaAI, Evaluator
Technopro.Othello.ViewModels
Technopro.Othello.WPF.Converters
Technopro.Othello.WinUI3.Converters
Technopro.Othello.Tests.*
```

---

## Claude Code カスタムコマンド

Claude Code（VS Code 拡張）のチャット欄でコマンドを入力すると、プロジェクト固有のガイドに沿って AI がタスクを実行します。  
コマンド定義ファイルは [.claude/commands/](.claude/commands/) に格納されています。

### 開発支援コマンド

| コマンド | 概要 | 引用先 |
|---|---|---|
| `/debug-ai` | AI バックエンドの動作確認・問題調査 | 本プロジェクト独自 |
| `/new-feature` | 新機能をレイヤー順（Core → Tests → ViewModel → XAML）に追加するガイド | 本プロジェクト独自 |
| `/update-style` | WPF / WinUI3 のカラーパレット・スタイルキーを確認しながら UI を変更するガイド | 本プロジェクト独自 |
| `/code-review` | 本プロジェクトの開発履歴から抽出した7カテゴリ＋dotnet/runtime レビュー基準（例外・パフォーマンス・スタイル・テスト）でコードレビュー | 本プロジェクトの修正履歴 ＋ [dotnet/runtime コードレビュースキル](https://zenn.dev/sator_imaging/articles/628625956abc18) |

#### `/debug-ai` の使い方

```
/debug-ai
```

起動後にバッジ（`AI: Rust` / `AI: Python` / `AI: C#`）と問題の症状を伝えると、  
IPC 通信・アルファベータ探索・パリティテストの順でデバッグ手順を提示します。

#### `/new-feature` の使い方

```
/new-feature
```

「どのような機能を追加しますか？」と聞かれるので、追加したい機能を説明します。  
Core → Tests → ViewModel → XAML の実装順でプランを作成します。

#### `/update-style` の使い方

```
/update-style
```

変更したいスタイルを伝えると、`AppColors.xaml` / `AppStyles.xaml` の対象キーと変更方法を案内します。

#### `/code-review` の使い方

```
/code-review                   # 最新コミット（HEAD~1..HEAD）を対象
/code-review main..HEAD        # main ブランチとの差分を対象
/code-review src/Othello.ViewModels/GameViewModel.cs   # ファイル指定
```

本プロジェクトの開発中に実際に発見・修正された問題をもとに、以下の7カテゴリでチェックします:

**セクション 1 — 本プロジェクトの修正履歴より（7カテゴリ）**

| カテゴリ | 主なチェック内容 | 由来コミット |
|---|---|---|
| リソース管理 | `IDisposable` 実装・`CancellationTokenSource` の更新順序 | Python プロセスリーク修正 |
| 非同期処理 | `async void` 禁止・stderr 非同期ドレイン・`CancellationToken` 競合防止 | `async void` → `async Task` 化、ReadLine デッドロック修正 |
| IPC 通信 | JSON 必須フィールドチェック・不正値バリデーション・プロトコル変更の両端反映 | `ai.py` フィールドチェック追加 |
| パフォーマンス | `HashSet` 化・`SolidColorBrush` キャッシュ・`depth==0` 早期 return・短絡評価 | `List` → `HashSet`、SolidColorBrush キャッシュ化 |
| ゲームロジック | Undo スナップショット方式・パス処理の集約・`IsInitialState` ソフトロック防止 | Undo スナップショット化、パス自動化 |
| 設計・テスタビリティ | AI ファクトリ注入・`InternalsVisibleTo`・定数化・パス一元管理 | AI 注入対応、`ConsoleInputParser` 分離 |
| XAML / UI | Converter の型一致・未使用リソース削除・WPF / WinUI3 両方への反映 | `InverseBooleanConverter` 追加、未使用スタイル削除 |

**セクション 2 — [dotnet/runtime コードレビュースキル](https://zenn.dev/sator_imaging/articles/628625956abc18) より（C# 一般、4カテゴリ）**

| カテゴリ | 主なチェック内容 |
|---|---|
| 例外・エラーハンドリング | `Debug.Assert` vs `ArgumentException`・`ThrowIf` ヘルパー・検証順序・例外握り潰し禁止 |
| パフォーマンス・割り当て | クロージャ回避・コレクション事前確保・アクセサキャッシュ・throw ヘルパー抽出・O(n²) 回避 |
| コードスタイル | `var` 制限・bool 引数名前付き・早期 return・`#region` 禁止・冗長初期化禁止 |
| テスト | バグ修正には回帰テスト必須・`[Theory]+[InlineData]`・具体的アサート・フレークテスト禁止 |

---

### セキュリティコマンド

| コマンド | 概要 | 引用先 |
|---|---|---|
| `/security-scan` | Rust CVE・ハードコード秘密情報・Python 危険パターン・NuGet 脆弱性の 4 項目を順番にスキャンし結果をレポート | [toshipon/claude-code-security-audit-skill](https://github.com/toshipon/claude-code-security-audit-skill) をベースに本プロジェクト向けカスタマイズ |
| `/diff-review` | `git diff` の差分を IPC・AI ロジック・入力処理・プロセス管理・依存関係の観点でセキュリティレビュー | [trailofbits/skills / differential-review](https://github.com/trailofbits/skills/tree/main/plugins/differential-review) をベースに本プロジェクト向けカスタマイズ |
| `/supply-chain-audit` | NuGet・cargo の依存ライブラリを CVE・メンテナー数・更新頻度・バージョン固定の観点でサプライチェーンリスク評価 | [trailofbits/skills / supply-chain-risk-auditor](https://github.com/trailofbits/skills/tree/main/plugins/supply-chain-risk-auditor) をベースに本プロジェクト向けカスタマイズ |
| `/semgrep-scan` | Python・C#・Rust コードの脆弱性パターンをスキャン（Semgrep がある場合は自動スキャン、なければパターンマッチで代替） | [trailofbits/skills / static-analysis](https://github.com/trailofbits/skills/tree/main/plugins/static-analysis) をベースに本プロジェクト向けカスタマイズ |

#### `/security-scan` の使い方

```
/security-scan
```

引数なし。4 項目を順番に検査し、`🔴 高リスク / 🟡 中リスク / 🟢 低リスク / ✅ 問題なし` の形式でレポートします。  
発見件数が 5 件以上の場合は `docs/security-scan-report-YYYY-MM-DD.md` への保存を提案します。

#### `/diff-review` の使い方

```
/diff-review                   # 最新コミット（HEAD~1..HEAD）を対象
/diff-review main..HEAD        # main ブランチとの差分を対象
/diff-review HEAD~3            # 直近 3 コミットを対象
```

差分の変更カテゴリを分類したうえで、C# / Python / Rust それぞれの言語固有チェックとテストカバレッジを評価します。

#### `/supply-chain-audit` の使い方

```
/supply-chain-audit
```

引数なし。`dotnet list package`・`cargo metadata`・`cargo audit` を実行し、  
各依存ライブラリの CVE・メンテナンス状態・メンテナー数・バージョン指定を評価してレポートします。

#### `/semgrep-scan` の使い方

```
/semgrep-scan
```

引数なし。Semgrep がインストールされている場合は `p/python` / `p/csharp` / `p/owasp-top-ten` ルールセットで自動スキャン、  
未インストールの場合はパターンマッチによる手動スキャンにフォールバックします。  
Semgrep のインストール: `pip install semgrep` または `winget install Semgrep.Semgrep`

---

## ライセンス

[MIT License](LICENSE)
