# Othello (オセロ) — Python AI × C# .NET 10

人間 vs CPU（Python AI）のオセロ（リバーシ）ゲームです。  
ゲームの UI・ロジックは C# (.NET 10) で実装し、AI 部分は Python（アルファベータ探索）で実装しています。  
両者は stdin/stdout の JSON 通信で連携します。

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
- Python によるアルファベータ探索（Alpha-Beta Pruning）
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

> `Othello_CSharpPython.code-workspace` を開くと推奨拡張機能のインストールが自動的に促されます。

### Python

| 項目 | 要件 |
|------|------|
| バージョン | 3.8 以上 |
| パッケージ | 標準ライブラリのみ（追加インストール不要） |
| Windows | Python Launcher（`py`）の使用を推奨 |

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
2. 初回起動時に推奨拡張機能（C# Dev Kit・Python）のインストールを促されるのでインストールする
3. ビルドタスクを実行する

| 操作 | 手順 |
|------|------|
| WPF ビルド | `Ctrl+Shift+B` |
| WinUI3 ビルド | `Ctrl+Shift+P` → `Tasks: Run Task` → `build: WinUI3` |
| テスト実行 | `Ctrl+Shift+P` → `Tasks: Run Test Task` |
| WPF 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WPF` |
| WinUI3 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WinUI3` |
| コンソール版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: Console` |
| dist へ発行（WPF） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WPF → dist/WPF` |
| dist へ発行（WinUI3） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: WinUI3 → dist/WinUI3` |
| dist へ発行（両方） | `Ctrl+Shift+P` → `Tasks: Run Task` → `publish: all → dist` |

> **ターミナルからも実行可能**: VS Code 内蔵ターミナル（`` Ctrl+` ``）でも dotnet CLI コマンドが使えます。

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
│   ├── Othello.Python/        # Python AI
│   │   ├── ai.py              # エントリポイント（stdin/stdout ループ）
│   │   ├── alpha_beta.py      # アルファベータ探索
│   │   ├── evaluator.py       # 盤面評価関数（位置重み + Mobility + 終局 depth 評価）
│   │   └── board.py           # 盤面操作ユーティリティ（has_any_flip / count_valid_moves 含む）
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
│  Othello.Python                             │
│  アルファベータ探索 + 盤面評価               │
└─────────────────────────────────────────────┘
```

### C# ↔ Python 通信（IPC）

C# から Python プロセスを起動し、stdin/stdout で JSON メッセージを 1 行ずつやり取りします。

```
C# (PythonSubprocessAI)                Python (ai.py)
        |                                     |
        |--- stdin: {"board":..., "player":1, "depth":5} -->|
        |                                     | alpha-beta 探索
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
