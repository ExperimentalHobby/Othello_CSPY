# Othello.AI/CSharp — C# フォールバック AI

Python が利用できない環境でのフォールバック AI です。`PythonSubprocessAI` が Python プロセスの起動に失敗した場合、`GameViewModel` は自動的にこの実装へ切り替えます。

> アルゴリズム・評価関数の仕様は [Python/README.md](../Python/README.md) の「5. アルゴリズム詳細」「6. 評価関数」と同一です。  
> IPC プロトコル・ハンドシェイクの詳細は [Python/README.md](../Python/README.md) を参照してください。  
> Rust 拡張のビルド手順は [Rust/README.md](../Rust/README.md) を参照してください。

---

## ファイル構成

| ファイル | 役割 |
|---------|------|
| `AlphaBetaAI.cs` | αβ探索エンジン本体。`IAIStrategy` を実装し、`GameViewModel` から呼ばれる |
| `Evaluator.cs` | 位置重みスコア・Mobility スコア・終局評価を計算する静的クラス |

---

## AlphaBetaAI

### 役割

αβ枝刈り（Alpha-Beta Pruning）+ Zobrist トランスポジションテーブルによる C# 純粋実装 AI。  
Python/Rust バックエンドと**同じアルゴリズム・評価関数**を使用するため、同じ盤面・深さで（ほぼ）同じ着手を選択します。

### 難易度別の探索方式

| 難易度 | 探索方式 | `depth` | `time_ms` |
|--------|---------|---------|-----------|
| イージー | 固定深さ | 2 | — |
| ノーマル | 固定深さ | 5 | — |
| ハード | 反復深化 | 最大 10 | 8000 ms |

- **固定深さ**（`GetBestMoveFixedDepth`）: Easy / Normal で使用。指定深さまで一度だけ探索。
- **反復深化**（`GetBestMoveIterativeDeepening`）: Hard で使用。深さ 1 から順に探索し、時間制限（8 秒）に達したら直前の完了深さの結果を採用。

### Zobrist トランスポジションテーブル

同一局面を再探索するコストを削減するキャッシュ機構です。

- キー: `ulong` 型の Zobrist ハッシュ（盤面状態から計算）
- 値: `TTEntry { Score, Depth, IsMaximizing, NodeType }`

`NodeType` によって境界値の再利用方法を制御します。

| NodeType | 意味 | 再利用条件 |
|----------|------|----------|
| `Exact` | αβ窓内で得た正確値 | そのまま返す |
| `LowerBound` | βカット（fail-high）で得た下界値 | `alpha = max(alpha, score)` |
| `UpperBound` | αを改善できなかった（fail-low）上界値 | `beta = min(beta, score)` |

### ムーブオーダリング

```csharp
moves.OrderByDescending(m => Evaluator.GetPositionWeight(m.Row, m.Column))
```

コーナー・辺など位置重みの高い手を優先探索することで枝刈り効率を向上させます。

---

## Evaluator

### 中盤評価（`Evaluate`）

```
評価値 = 位置重みスコア + Mobility スコア（50 手未満）
       = 位置重みスコア + 石数差スコア × 10（50 手以上・終盤）
```

**位置重みテーブル（`EvaluationWeights`）:**

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

| マス | 重み | 理由 |
|------|------|------|
| コーナー | +100 | 取り返せない最重要マス |
| X-square（コーナー斜め隣） | −50 | コーナーを相手に献上するリスク |
| C-square（コーナー辺隣） | −20 | 同様のリスク |
| 辺 | +10 | 比較的安定 |

**Mobility スコア（50 手未満のみ）:**

```
(AI の有効手数 − 相手の有効手数) × 10
```

50 手以上（終盤）は石数差 × 10 に切り替えます。

### 終局評価（`EvaluateFinal`）

| 結果 | 返す値 |
|------|--------|
| 勝利 | `+10000 + depth`（早い勝ちを選好） |
| 引き分け | `0` |
| 敗北 | `−10000 − depth`（遅い負けを選好） |

中盤評価値（最大でも数百程度）と桁を大きく離すことで、終局の勝敗を常に優先します。

---

## テスト

C# AI のテストは `src/Othello.Tests/AI/Core/AlphaBetaAITests.cs` にあります。

```bash
dotnet test src/Othello.Tests/Othello.Tests.csproj
```

主なテストケース:

| テストクラス | 内容 |
|------------|------|
| `GetBestMoveTests` | 初期盤面での合法手返却、連続呼び出しで常に合法手 |
| `GetBestMoveIterativeDeepeningTests` | Hard モードで時間制限内に合法手を返す |
| `EvaluatorTests` | 位置重み・石数差・Mobility・終局評価の正確性 |
