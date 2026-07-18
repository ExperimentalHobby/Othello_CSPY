このプロジェクトに新機能を追加します。以下の順序で進めてください:

## アーキテクチャ概要

```
C# (WPF / WinUI3 / Console)
  └─ GameViewModel (src/Othello.ViewModels/)
       └─ IAIStrategy
            ├─ PythonSubprocessAI  → Python subprocess → alpha_beta.py
            │                                              ├─ othello_ai_rust (Rust/PyO3) ← 優先
            │                                              └─ alpha_beta_py (純Python)   ← フォールバック
            └─ AlphaBetaAI (C#)                                                          ← Python不在時
```

## 追加手順

1. **Othello.Core にロジックを追加**
   - 新しいモデル → `src/Othello.Core/Models/`
   - ルール変更   → `src/Othello.Core/Rules/OthelloRules.cs` または `FlipCalculator.cs`
   - ゲームフロー → `src/Othello.Core/Game/GameEngine.cs`

2. **Othello.Tests にテストを追加**
   - テストを先に書き、実装後に `dotnet test Othello.slnx` で全テストが通ることを確認

3. **GameViewModel を更新（UI フィードバックが必要な場合）**
   - `src/Othello.ViewModels/GameViewModel.cs` にプロパティやコマンドを追加
   - `SetProperty()` で INotifyPropertyChanged を通知する

4. **XAML を更新（見た目の変更が必要な場合）**
   - WPF:   `src/Othello.GUI/WPF/MainWindow.xaml`（リソースは `Resources/AppColors.xaml` / `AppStyles.xaml` で管理）
   - WinUI3: `src/Othello.GUI/WinUI3/MainWindow.xaml`
   - 色はリソースキーを使用（`{StaticResource BoardGreenBrush}` など）

## カラーパレット（WPF）

| 用途               | キー                      | カラーコード |
|--------------------|---------------------------|-------------|
| ウィンドウ背景     | `WindowBackgroundBrush`   | `#2d5016`   |
| メニュー・サイドバー | `MenuDarkGreenBrush`     | `#1a3010`   |
| ボード・ボタン     | `BoardGreenBrush`         | `#4a7c2f`   |
| ホバー             | `ButtonHoverGreenBrush`   | `#5a8c3f`   |
| 合法手ハイライト   | `ValidMoveHighlightBrush` | `Yellow`    |

どのような機能を追加しますか？
