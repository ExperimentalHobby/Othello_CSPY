# Othello.AI — AI 層 概要

このディレクトリは、オセロ AI の **三段階フォールバック構成**を実装します。

```
GameViewModel
  └─ PythonSubprocessAI（Python あり）              → AI: Rust / AI: Python
       └─ ai.py → alpha_beta.py
               ├─ othello_ai_rust（Rust ビルド済み）   → AI: Rust
               └─ alpha_beta_py.py（Rust 未ビルド）   → AI: Python
  └─ AlphaBetaAI（Python なし）                      → AI: C#
```

バックエンドの判別は Python プロセス起動直後の**ハンドシェイク**（`ai.py` が出力する `{"backend": "rust" | "python"}`）で行い、ファイル存在チェックではなく実際の `import` 結果を反映します。

---

## バックエンド一覧

| バッジ | フォルダ | 説明 |
|--------|---------|------|
| **AI: Rust** | [Rust/](Rust/) | PyO3 製 Rust 拡張（`othello_ai_rust`）。最も高速 |
| **AI: Python** | [Python/](Python/) | 純 Python フォールバック。Rust 未ビルドまたは import 失敗時 |
| **AI: C#** | [CSharp/](CSharp/) | C# 純粋実装。Python 未インストール時のフォールバック |

---

## 共通仲介層（Core/）

[Core/](Core/) ディレクトリは全バックエンドが共有するインターフェース・ユーティリティを提供します。

| ファイル | 役割 |
|---------|------|
| `IAIStrategy.cs` | AI バックエンドの共通インターフェース（`GetBestMove` / `EngineName`） |
| `DifficultyLevel.cs` | 難易度列挙型と拡張メソッド（`GetSearchDepth` / `GetTimeLimitMs`） |
| `PythonSubprocessAI.cs` | Python サブプロセスを起動・管理し IPC 通信を担う実装 |
| `AiScriptPaths.cs` | `ai.py` のパスを一元管理 |

---

## IPC プロトコル概要（C# ↔ Python）

Python プロセスは 1 ゲームにつき 1 プロセス起動されます。

```
[起動時]
C# ─ py -u ai.py を起動 ──────────────────────────────→ Python (ai.py)
C# ←─ stdout: {"backend": "rust"} ─────────────────── (ハンドシェイク)
  ↑ EngineName を確定（"AI: Rust" / "AI: Python"）

[1 手ごとに繰り返し]
C# ─ stdin: {"board":..., "player":1, "depth":5, "time_ms":null} ──→ Python
C# ←─ stdout: {"row":2, "col":3} ───────────────────────────────── Python

[終局・新規ゲーム]
C# ─ stdin を閉じる ──────────────────────────────────→ Python (exit 0)
```

- `time_ms` は Hard 難易度時のみ `8000`（ms）が付加され、Python/Rust 側で反復深化探索を使用します。それ以外は `null`（固定深さ探索）です。
- C# フォールバック AI（`AlphaBetaAI`）も Hard では同じ 8 秒制限の反復深化を使用します。

---

## 詳細ドキュメント

各バックエンドのアルゴリズム・評価関数・ビルド手順・テスト方法など詳細は各フォルダの README を参照してください。

| フォルダ | README |
|---------|--------|
| [CSharp/](CSharp/) | [CSharp/README.md](CSharp/README.md) |
| [Python/](Python/) | [Python/README.md](Python/README.md) |
| [Rust/](Rust/) | [Rust/README.md](Rust/README.md) |

---

## GPU 化の検討

### 現状: 完全 CPU 動作

全バックエンド（Rust・Python・C#）ともにアルゴリズムは**アルファベータ法（Alpha-Beta Pruning）+ 反復深化**であり、CPU 上での逐次処理です。

### アルファベータ法が GPU に向かない理由

アルファベータ探索は本質的に**逐次依存**の処理です。

- 各ノードの探索結果が親の α/β 値を更新してから枝刈りを行うため、ノード間に依存関係がある
- 探索木は**不規則な形状**（深さ・幅がノードごとに異なる）→ GPU の強みである「均一な並列処理」と相性が悪い
- Othello の 8×8 盤面は小さく、GPU のオーバーヘッド（データ転送など）が利益を上回る可能性が高い

### GPU 活用の選択肢

| アプローチ | 概要 | 難易度 | 強さ向上 |
|---|---|---|---|
| **A. NN 評価関数 + CPU 探索** | 手作りの位置重みを CNN で置き換え、葉ノードの評価のみ GPU に投げる。探索（α-β）は CPU のまま（PyTorch / ONNX Runtime） | 高 | 大 |
| **B. MCTS + GPU 並列シミュレーション** | アルファベータを廃止し MCTS（モンテカルロ木探索）に変更。多数のランダム対局を GPU で並列実行。AlphaZero 的アプローチ | 高 | 大 |
| **C. GPU 並列アルファベータ（YBWC 等）** | 研究的手法。枝刈りを一部犠牲にして並列度を上げる | 非常に高 | 限定的 |

### 結論

現行の **Rust + アルファベータ（CPU）は本プロジェクトの規模に対して最適解**です。Hard 難易度（深さ 10・8 秒制限）でも Rust 版は実用上十分な速度で動作します。

GPU 化が有効なのは、自己対戦学習でモデルを継続的に強化したい・AlphaZero 的アプローチを試したいといった研究・発展的な目的がある場合に限られます。その際は **アプローチ A（NN 評価関数）** が最も現実的な出発点となります。ただし PyTorch 等の外部依存追加が必要になるため、現在の「標準ライブラリのみ」方針との整合を検討してください。
