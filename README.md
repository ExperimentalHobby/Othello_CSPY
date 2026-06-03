# Othello (オセロ) — Python AI × C# .NET 10

人間 vs CPU（Python AI）のオセロ（リバーシ）ゲームです。  
ゲームの UI・ロジックは C# (.NET 10) で実装し、AI 部分は Python（アルファベータ探索）で実装しています。  
両者は stdin/stdout の JSON 通信で連携します。

## 機能

### ゲームモード
- **WPF 版**: グラフィカルな盤面でマウス操作（Windows専用）
- **コンソール版**: ターミナル上でテキスト形式のゲームプレイ（クロスプラットフォーム）

### ゲームプレイ
- 人間の担当色を選択可能（黒＝先手 / 白＝後手）
- 難易度 3 段階（イージー / ノーマル / ハード）
- 有効手のハイライト表示（WPF 版：黄色、コンソール版：`*`）
- パス・1手戻す（Undo）機能

### AI
- Python によるアルファベータ探索（Alpha-Beta Pruning）
- ムーブオーダリング（位置重みによる事前ソート）で探索効率を向上
- 難易度別探索深さ: イージー=2、ノーマル=5、ハード=10

## システム要件

| 項目 | 要件 |
|------|------|
| OS | Windows 10/11（WPF 版）、または任意の OS（コンソール版） |
| .NET | .NET 10 SDK |
| Python | 3.8 以上（標準ライブラリのみ、追加インストール不要） |

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
# ビルド
dotnet build

# WPF 版を起動
dotnet run --project src/Othello.WPF/Othello.WPF.csproj

# コンソール版を起動
dotnet run --project src/Othello.Console/Othello.Console.csproj
```

---

#### Visual Studio 2026

**インストール時に必要なワークロード（Visual Studio Installer で選択）:**

| ワークロード | 用途 |
|---|---|
| .NET デスクトップ開発 | WPF・コンソール・テストのビルドに必要 |

> **.NET 10 SDK** が含まれていない場合は、「個別のコンポーネント」タブで `.NET 10.0 Runtime` を追加インストールしてください。

1. `Othello.sln` をダブルクリックして Visual Studio で開く
2. メニュー **ビルド → ソリューションのビルド**（`Ctrl+Shift+B`）
3. 起動するプロジェクトを右クリック → **スタートアッププロジェクトに設定**
   - WPF 版: `Othello.WPF`
   - コンソール版: `Othello.Console`
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
| ビルド | `Ctrl+Shift+B` |
| テスト実行 | `Ctrl+Shift+P` → `Tasks: Run Test Task` |
| WPF 版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: WPF` |
| コンソール版を起動 | `Ctrl+Shift+P` → `Tasks: Run Task` → `run: Console` |

> **ターミナルからも実行可能**: VS Code 内蔵ターミナル（`` Ctrl+` ``）でも dotnet CLI コマンドが使えます。

## 操作方法

### WPF 版
| 操作 | 内容 |
|------|------|
| 黄色のマスをクリック | 石を置く |
| 新規ゲーム ボタン | ゲームを最初からやり直す |
| 戻す ボタン | 直前の手を 1 手取り消す |
| パス ボタン | パスする（置ける場所がない場合） |
| 難易度 コンボボックス | AI の強さを変更（次のゲームから適用） |
| あなたの色 コンボボックス | 担当色を変更（次のゲームから適用） |

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
│   ├── Othello.WPF/           # WPF UI 層（.NET 10、Windows専用）
│   │   ├── ViewModels/        # GameViewModel, BoardSquareViewModel
│   │   ├── Converters/        # BoolToVisibilityConverter
│   │   └── MainWindow.xaml
│   │
│   ├── Othello.Console/       # コンソール版（.NET 10）
│   │   └── Program.cs
│   │
│   ├── Othello.Python/        # Python AI
│   │   ├── ai.py              # エントリポイント（stdin/stdout ループ）
│   │   ├── alpha_beta.py      # アルファベータ探索
│   │   ├── evaluator.py       # 盤面評価関数
│   │   └── board.py           # 盤面操作ユーティリティ
│   │
│   └── Othello.Tests/         # xUnit テスト（.NET 10）
│
├── Othello.sln
├── LICENSE
└── README.md
```

## アーキテクチャ

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

## 名前空間

```
Technopro.Othello.Core.Models
Technopro.Othello.Core.Rules
Technopro.Othello.Core.Game
Technopro.Othello.Core.AI
Technopro.Othello.WPF.ViewModels
Technopro.Othello.WPF.Converters
Technopro.Othello.Tests.Core.*
```

## ライセンス

[MIT License](LICENSE)
