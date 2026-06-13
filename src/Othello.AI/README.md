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
