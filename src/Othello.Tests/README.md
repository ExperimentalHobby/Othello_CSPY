# Othello.Tests

`Othello.Core` のゲームロジック全体を検証する xUnit テストプロジェクト。

## 実行方法

```bash
# 全テスト実行
dotnet test

# 詳細出力
dotnet test --verbosity detailed

# runsettings を明示指定する場合
dotnet test --settings .runsettings
```

## テスト内訳

| グループ | テスト数 | 内容 |
|---------|---------|------|
| Models | 11 | Board (4), Position (5), PlayerColor (3) の動作 |
| Rules | 8 | OthelloRules (6), FlipCalculator (2) の判定ロジック |
| Game | 11 | GameEngine の着手・Pass・Undo・終局処理 |
| AI テスト | — | Python AI は Python スクリプト単体で確認（下記参照） |

## テスト設定（.runsettings）

リポジトリルートの `.runsettings` でテスト実行を制御している。

| 設定 | 値 | 内容 |
|------|-----|------|
| `TestSessionTimeout` | 30000 ms | テストセッション全体の上限時間 |
| `MaxCpuCount` | 0（全コア） | 並列実行に使用する CPU コア数 |
| `ParallelizeAssembly / TestClasses` | true | アセンブリ・クラス単位の並列化 |
| `CodeCoverage → ModulePath` | `Othello.Core` | カバレッジ収集対象を Core に限定 |

## Python AI の単体確認

C# のテストには含まれない Python AI の動作確認は以下のコマンドで行う。

```bash
echo '{"board":[[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,2,1,0,0,0],[0,0,0,1,2,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0]],"player":1,"depth":5}' | py src/Othello.Python/ai.py
```
