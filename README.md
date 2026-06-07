# Othello (オセロ) — Python AI × C# .NET 10

人間 vs CPU（AI）のオセロ（リバーシ）ゲームです。  
ゲームの UI・ロジックは C# (.NET 10)、AI（アルファベータ探索）は **Rust** で実装しています。  
C# ↔ Python は stdin/stdout の JSON 通信で連携し、Python は AI 計算を Rust 拡張（PyO3）へ委譲します（**C# → Python → Rust**）。  
Rust 拡張が未ビルドの環境では、純 Python 実装に自動でフォールバックします（挙動は同一、速度のみ変化）。

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

### AI
- アルファベータ探索（Alpha-Beta Pruning）を **Rust（PyO3 拡張）** で実装。Python が探索を Rust へ委譲する
- Rust 拡張が無い環境では**純 Python 実装に自動フォールバック**（着手選択は同一、速度のみ変化）
- ムーブオーダリング（位置重みによる事前ソート）で探索効率を向上
- 難易度別探索深さ: イージー=2、ノーマル=5、ハード=10
- 終局評価に残り探索深さを加味し、早い勝ちを選好・長引く負けを回避

## システム要件

| 項目 | WPF 版 / WinUI3 版 | コンソール版 |
|------|-------------------|------------|
| OS | Windows 10/11 | 任意の OS |
| .NET | .NET 10 SDK | .NET 10 SDK |
| Python | 3.8 以上 | 3.8 以上 |
| Windows App Runtime | WinUI3 版のみ: 2.0 以上 | 不要 |
| Rust ツールチェーン | 任意（AI を高速化する場合のみ。未ビルドなら純 Python で動作） | 同左 |

> **Windows App Runtime のインストール（WinUI3 版のみ）**
> ```
> winget install Microsoft.WindowsAppRuntime.2.0 --accept-package-agreements --accept-source-agreements
> ```

## 開発環境

以下のいずれかの IDE / エディタを使用してください。

### Visual Studio 2022 以降

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
| PlantUML | `jebbs.plantuml` | README の PlantUML 図をプレビュー |
| Even Better TOML | `tamasfe.even-better-toml` | `Cargo.toml` / `pyproject.toml` の構文ハイライト・バリデーション |

> `Othello_CSharpPython.code-workspace` を開くと推奨拡張機能のインストールが自動的に促されます。

### Python

| 項目 | 要件 |
|------|------|
| バージョン | 3.8 以上 |
| パッケージ | 標準ライブラリのみ（追加インストール不要） |
| Windows | Python Launcher（`py`）の使用を推奨 |

### Rust（任意・AI 高速化）

AI（アルファベータ探索）を高速な Rust 実装に置き換える場合のみ必要です。  
ビルドしなくても純 Python 実装で動作するため、**この環境構築は任意**です。

| 項目 | 要件 / 導入例 |
|------|------|
| Rust | [rustup](https://rustup.rs/) で stable を導入 |
| maturin | `py -m pip install --user maturin`（PyO3 拡張のビルドツール） |
| C リンカ | Windows: [VS Build Tools](https://visualstudio.microsoft.com/visual-cpp-build-tools/) の「C++ によるデスクトップ開発」ワークロード（`link.exe`）／ Linux: `gcc` など |

> **VS Build Tools のインストール（Windows）**
> [Build Tools for Visual Studio](https://visualstudio.microsoft.com/visual-cpp-build-tools/) からインストーラーを入手し、「**C++ によるデスクトップ開発**」ワークロードを選択してください。コマンドラインからは次でも導入できます。
> ```
> winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --quiet" --accept-package-agreements --accept-source-agreements
> ```

> ビルド手順はセットアップの「3. AI 拡張（Rust）のビルド」を参照してください。

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
# WPF 版ビルド
dotnet build Othello.WPF.sln

# WinUI3 版ビルド
dotnet build Othello.WinUI3.sln

# WPF 版を起動
dotnet run --project src/Othello.WPF/Othello.WPF.csproj

# WinUI3 版を起動
dotnet run --project src/Othello.WinUI3/Othello.WinUI3.csproj

# コンソール版を起動
dotnet run --project src/Othello.Console/Othello.Console.csproj
```

---

#### Visual Studio

1. `Othello.WPF.sln`（WPF 版）または `Othello.WinUI3.sln`（WinUI3 版）をダブルクリックして開く
2. メニュー **ビルド → ソリューションのビルド**（`Ctrl+Shift+B`）
3. 起動するプロジェクトを右クリック → **スタートアッププロジェクトに設定**
4. **デバッグ → デバッグの開始**（`F5`）または **デバッグなしで開始**（`Ctrl+F5`）

> **テスト実行**: メニュー **テスト → すべてのテストを実行**（`Ctrl+R, A`）

---

#### Visual Studio Code

1. `Othello_CSharpPython.code-workspace` をダブルクリック、または以下のコマンドで開く
   ```bash
   code Othello_CSharpPython.code-workspace
   ```
2. 初回起動時に推奨拡張機能のインストールを促されるのでインストールする
3. ビルドタスクを実行する

| 操作 | 手順 |
|------|------|
| WPF ビルド | `Ctrl+Shift+B` |
| WinUI3 ビルド | `Ctrl+Shift+P` → `Tasks: Run Task` → `build: WinUI3` |
| AI 拡張（Rust）をビルド | `Ctrl+Shift+P` → `Tasks: Run Task` → `build: Rust AI` |
| テスト実行（.NET） | `Ctrl+Shift+P` → `Tasks: Run Test Task` |
| Rust テスト | `Ctrl+Shift+P` → `Tasks: Run Task` → `test: Rust` |
| Python テスト | `Ctrl+Shift+P` → `Tasks: Run Task` → `test: Python` |
| WPF 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WPF` |
| WinUI3 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WinUI3` |
| コンソール版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: Console` |
| dist へ発行（WPF） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WPF → dist/WPF` |
| dist へ発行（WinUI3） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WinUI3 → dist/WinUI3` |
| dist へ発行（両方） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: all → dist` |

> `publish: *` タスクは発行前に自動で `build: Rust AI` を実行し、Rust 製 AI を dist に同梱します（Rust・maturin・C リンカが前提。未導入の環境では Rust ビルドで失敗するため、その場合は先に [Rust（任意・AI 高速化）](#rust任意ai-高速化) を導入してください）。
>
> 一方、`Ctrl+Shift+B`（`build: WPF`）など通常のビルド／起動タスクは Rust をビルドせず、既存の `.pyd` があればそれを使います（無ければ純 Python で動作）。Rust を更新したいときだけ `build: Rust AI` を実行してください。

> **ターミナルからも実行可能**: VS Code 内蔵ターミナル（`` Ctrl+` ``）でも dotnet CLI コマンドが使えます。

---

### 3. AI 拡張（Rust）のビルド（任意・高速化）

AI のアルファベータ探索を Rust 実装（[src/Othello.Rust/](src/Othello.Rust/)）へ委譲することで高速化できます。  
**ビルドしない場合でも純 Python 実装で動作する**ため、この手順は任意です（前提ツールは [Rust（任意・AI 高速化）](#rust任意ai-高速化) を参照）。

**ビルド（リポジトリルートから）:**

```powershell
pwsh -File src/Othello.Rust/build_rust.ps1
```

成功すると拡張モジュール `othello_ai_rust.pyd`（Windows）/ `othello_ai_rust*.so`（Linux・macOS）が
`src/Othello.Python/` に配置されます。以降の `dotnet build` で出力ディレクトリへ自動コピーされ、
実行時に Python が Rust 実装を読み込みます（読み込めない場合は純 Python にフォールバック）。

> 生成物（`.pyd`/`.so`）は OS・アーキテクチャ依存のためリポジトリには含めません（`.gitignore` 済み）。環境ごとにビルドしてください。

**テスト:**

```powershell
# Rust 単体テスト（Python 非依存）
cargo test --manifest-path src/Othello.Rust/Cargo.toml --no-default-features

# Python テスト（Rust 経路 + Rust↔Python 整合性テスト。Rust 未ビルド時は整合性テストは自動スキップ）
py -m unittest discover -s src/Othello.Python -p "test_*.py"
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
│   │   ├── Game/              # GameEngine
│   │   └── AI/                # IAIStrategy, DifficultyLevel, PythonSubprocessAI
│   │
│   ├── Othello.ViewModels/    # 共有 ViewModel 層（.shproj 共有プロジェクト）
│   │   ├── ViewModelBase.cs
│   │   ├── RelayCommand.cs    # #if WPF で CommandManager と手動通知を切り替え
│   │   ├── BoardSquareViewModel.cs
│   │   └── GameViewModel.cs
│   │
│   ├── Othello.WPF/           # WPF UI 層（.NET 10、Windows 専用）
│   │   ├── Converters/        # BoolToVisibilityConverter, InverseBooleanConverter, PlayerColorToBrushConverter
│   │   └── MainWindow.xaml
│   │
│   ├── Othello.WinUI3/        # WinUI3 UI 層（.NET 10、Windows 専用）
│   │   ├── Converters/        # BoolToVisibilityConverter, PlayerColorToBrushConverter
│   │   ├── Program.cs         # Bootstrap.TryInitialize による明示的な Runtime 初期化
│   │   ├── App.xaml
│   │   └── MainWindow.xaml
│   │
│   ├── Othello.Console/       # コンソール版（.NET 10）
│   │   └── Program.cs
│   │
│   ├── Othello.Python/        # Python AI（C# との窓口 + フォールバック）
│   │   ├── ai.py              # エントリポイント（stdin/stdout ループ）
│   │   ├── alpha_beta.py      # バックエンド選択シム（Rust 優先・Python フォールバック）
│   │   ├── alpha_beta_py.py   # 純 Python アルファベータ探索（フォールバック実装）
│   │   ├── evaluator.py       # 盤面評価関数（位置重み + Mobility + 終局 depth 評価）
│   │   └── board.py           # 盤面操作ユーティリティ（has_any_flip / count_valid_moves 含む）
│   │
│   ├── Othello.Rust/          # Rust 製 AI 拡張（PyO3 / abi3）
│   │   ├── src/lib.rs         # アルファベータ探索・評価・盤面操作（Python 版と挙動一致）
│   │   ├── Cargo.toml
│   │   ├── pyproject.toml     # maturin ビルド設定
│   │   └── build_rust.ps1     # ビルド & .pyd/.so 配置ヘルパー
│   │
│   └── Othello.Tests/         # xUnit テスト（.NET 10）
│
├── Othello.WPF.sln            # WPF 版ソリューション
├── Othello.WinUI3.sln         # WinUI3 版ソリューション
├── LICENSE
└── README.md
```

---

## アーキテクチャ

### レイヤー構成

```
┌─────────────────────────────────────────────┐
│        UI 層                                 │
│  Othello.WPF  /  Othello.WinUI3             │
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
                   │ stdin/stdout JSON
┌──────────────────▼──────────────────────────┐
│        AI 層                                 │
│  Othello.Python … ai.py / バックエンド選択   │
│        │ 委譲（未ビルド時は純 Python に戻る） │
│        ▼                                     │
│  Othello.Rust（PyO3 拡張）                   │
│  アルファベータ探索 + 盤面評価               │
└─────────────────────────────────────────────┘
```

### C# ↔ Python 通信（IPC）

C# から Python プロセスを起動し、stdin/stdout で JSON メッセージを 1 行ずつやり取りします。

```
C# (PythonSubprocessAI)                Python (ai.py)
        |                                     |
        |--- stdin: {"board":..., "player":1, "depth":5} -->|
        |                                     | Rust 拡張で alpha-beta 探索
        |                                     | （未ビルド時は純 Python）
        |<-- stdout: {"row":2, "col":3} ------|
```

Python プロセスは 1 ゲームにつき 1 つ起動され、ゲーム終了時に停止します。

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
- Mobility（+10/手）: 着手の選択肢が多いほど有利

---

## 名前空間

```
Technopro.Othello.Core.Models
Technopro.Othello.Core.Rules
Technopro.Othello.Core.Game
Technopro.Othello.Core.AI
Technopro.Othello.ViewModels
Technopro.Othello.WPF.Converters
Technopro.Othello.WinUI3.Converters
Technopro.Othello.Tests.Core.*
```

---

## ライセンス

[MIT License](LICENSE)
