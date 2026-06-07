# Othello.Rust

オセロ AI の探索本体を Rust で実装した PyO3 拡張クレート。  
Python 層（`src/Othello.Python/`）が `othello_ai_rust.get_best_move()` を呼び出すことで、
アルファベータ探索を高速に実行します。Rust 拡張が未ビルドの環境では純 Python 実装に自動フォールバックします。

アルゴリズム・評価関数・IPC プロトコルの詳細は [src/Othello.Python/README.md](../Othello.Python/README.md) を参照してください。

---

## 前提ツール

| ツール | 用途 |
|--------|------|
| [Rust toolchain](https://rustup.rs/) | `cargo build` / `cargo test` |
| [maturin](https://github.com/PyO3/maturin) | PyO3 拡張のビルド（`py -m pip install --user maturin`） |
| MSVC `link.exe`（Windows） | C リンカ（VS Build Tools に同梱） |
| Python 3.8 以上 | maturin のビルド時に 1 つ必要 |

---

## ビルド手順

```powershell
# リポジトリルートから実行
pwsh -File src/Othello.Rust/build_rust.ps1
```

スクリプトが行うこと:

1. `maturin build --release` で abi3 ホイール（`.whl`）を生成
2. ホイール内の拡張モジュール（`othello_ai_rust.pyd` / `othello_ai_rust*.so`）を `src/Othello.Python/` へ配置
3. 以降の `dotnet build` が `.pyd`/`.so` を出力ディレクトリへコピーし、実行時に Python が Rust 実装を import する

---

## テスト

Rust ロジックの単体テストは `src/lib.rs` の `#[cfg(test)]` モジュールに記述されています。
PyO3 を切り離したピュア Rust として実行するため `--no-default-features` を指定します。

```bash
cargo test --manifest-path src/Othello.Rust/Cargo.toml --no-default-features
```

Python と Rust の着手一致確認は `src/Othello.Python/test_parity.py` で行います（Rust 未ビルド時は自動スキップ）。

```bash
py -m unittest discover -s src/Othello.Python -p "test_*.py"
```

---

## 探索シーケンス

```plantuml
@startuml
title Rust アルファベータ探索シーケンス

participant "Python\n(alpha_beta.py)" as PY
participant "best_move()" as BM
participant "alpha_beta()\n【再帰】" as AB
participant "evaluate()" as EV
participant "evaluate_final()" as EF

PY -> BM : get_best_move(board, player, depth)
activate BM

BM -> BM : get_valid_moves(board, player)\nムーブオーダリング（位置重み降順ソート）

loop 各有効手 (r, c)
    BM -> BM : make_move(board, r, c, player)
    BM -> AB : alpha_beta(next_board, depth-1,\nα, β, is_maximizing=false, player)
    activate AB

    loop 探索ノード（depth > 0 かつ有効手あり）
        AB -> AB : get_valid_moves() → make_move()\nα/β 更新・枝刈り判定\n（α≥β でカット）
    end

    alt depth == 0（葉ノード）
        AB -> EV : evaluate(board, player)
        activate EV
        EV --> AB : 位置重みスコア差\n+ Mobility差 × 10
        deactivate EV
    else 両者とも有効手なし（終局）
        AB -> EF : evaluate_final(board, player, depth)
        activate EF
        EF --> AB : 勝ち: +(10000+depth)\n負け: -(10000+depth)\n引き分け: 0
        deactivate EF
    else 現在プレイヤーのみ有効手なし（パス）
        AB -> AB : alpha_beta(board, depth-1,\nα, β, !is_maximizing, player)
    end

    AB --> BM : スコア
    deactivate AB

    BM -> BM : best_score 更新\n（同点は先に見つかった手を維持）
end

BM --> PY : Option<(row, col)>\n（有効手なしは None）
deactivate BM
@enduml
```

---

## ファイル構成

| ファイル | 内容 |
|---------|------|
| `src/lib.rs` | 探索・評価・盤面操作の全実装 + 単体テスト |
| `Cargo.toml` | クレート定義。`python` フィーチャで PyO3 バインディングを有効化 |
| `pyproject.toml` | maturin ビルド設定（abi3-py38） |
| `build_rust.ps1` | ビルド & 拡張モジュール配置ヘルパー（PowerShell） |
