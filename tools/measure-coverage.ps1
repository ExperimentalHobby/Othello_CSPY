# カバレッジ計測スクリプト
# 使い方: .\measure-coverage.ps1
# 出力先: report/YYYYMMDD-HHMMSS_<ブランチ名>_report/

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ブランチ名を取得（スラッシュはハイフンに変換）
$branch = (git branch --show-current) -replace '/', '-'
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportDir = "report/${timestamp}_${branch}_report"
$tmpDir = "report/.tmp_coverage"

Write-Host "カバレッジ計測を開始します..."
Write-Host "出力先: $reportDir"

# 一時ディレクトリを準備
if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force }
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null

# テスト実行 + カバレッジ収集
Write-Host ""
Write-Host ">>> dotnet test (カバレッジ収集中)..."
dotnet test src/Othello.Tests/Othello.Tests.csproj `
    --collect:"XPlat Code Coverage" `
    --results-directory $tmpDir `
    --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "テストが失敗しました。レポートの生成を中止します。"
    exit 1
}

# coverage.cobertura.xml を検索
$xmlPath = Get-ChildItem -Path $tmpDir -Filter "coverage.cobertura.xml" -Recurse |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $xmlPath) {
    Write-Error "coverage.cobertura.xml が見つかりません。"
    exit 1
}

# ReportGenerator でレポートを生成
Write-Host ""
Write-Host ">>> ReportGenerator でレポートを生成中..."
reportgenerator `
    -reports:"$xmlPath" `
    -targetdir:"$reportDir" `
    -reporttypes:"Html;TextSummary"

# 一時ディレクトリを削除
Remove-Item $tmpDir -Recurse -Force

# サマリーを表示
Write-Host ""
Write-Host "=== カバレッジサマリー ==="
Get-Content "$reportDir/Summary.txt"
Write-Host ""
Write-Host "HTMLレポート: $reportDir/index.html"
