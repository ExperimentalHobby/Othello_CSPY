AI の着手に問題がある場合のデバッグ手順です。

## AI バックエンドの確認

アプリ起動時にメニューバー左端のバッジで使用中のバックエンドを確認できます:
- **AI: Rust** — PyO3 製 Rust 拡張（`Othello.Python/othello_ai_rust.pyd`）が使用されている
- **AI: Python** — 純 Python 実装（`src/Othello.AI/Python/alpha_beta_py.py`）が使用されている
- **AI: C#** — Python が見つからず C# フォールバック AI（`src/Othello.AI/CSharp/AlphaBetaAI.cs`）が使用されている

## 調査ポイント

**C# AlphaBetaAI** (`src/Othello.AI/CSharp/AlphaBetaAI.cs`)
- 探索深さが難易度に応じて正しく設定されているか (Easy:2, Medium:5, Hard:10)
- αβカットオフ・トランスポジションテーブルが正しく機能しているか

**Evaluator** (`src/Othello.AI/CSharp/Evaluator.cs`)
- 評価値の重み: コーナー +100, X マス -50, 着手可能数 ×10
- `EvaluateFinal` が終局時に正しい勝敗スコアを返しているか

**Python AI** (`src/Othello.AI/Python/`)
- `alpha_beta.py`: Rust/Python の選択ロジック
- `alpha_beta_py.py`: 純 Python 実装
- `test_parity.py`: Rust 版と Python 版の着手一致を検証するパリティテスト

## デバッグ手順

1. C# テスト (`src/Othello.Tests/AI/Core/PythonSubprocessAITests.cs`) で IPC 通信を確認
2. Python テスト: `py -m unittest discover -s src/Othello.AI/Python -p "test_*.py"`
3. parity テスト: `py src/Othello.AI/Python/test_parity.py` で Rust/Python の一致を確認
4. Rust ビルド: `pwsh -File src/Othello.AI/Rust/build_rust.ps1`

どのような AI の動作が問題ですか？
