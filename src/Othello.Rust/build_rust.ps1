<#
.SYNOPSIS
    Rust 製 AI 拡張(othello_ai_rust)をビルドし、Python から import 可能な場所へ配置する。

.DESCRIPTION
    1. maturin で abi3 ホイール(.whl)を release ビルドする
    2. ホイール内の拡張モジュール(othello_ai_rust.pyd / othello_ai_rust*.so)を取り出し、
       src/Othello.Python/ へ配置する（ai.py の作業ディレクトリ＝sys.path 上）

    配置後、C# 側の `dotnet build` が *.pyd/*.so を出力ディレクトリへコピーし、
    実行時に Python が Rust 実装を import する（未配置なら純 Python 実装にフォールバック）。

.NOTES
    前提: rustup(Rust)・maturin・C リンカ(Windows は VS Build Tools の MSVC link.exe) が導入済みであること。
    純 Rust ロジックのテストのみ行う場合:
        cargo test --manifest-path src/Othello.Rust/Cargo.toml --no-default-features

.EXAMPLE
    pwsh -File src/Othello.Rust/build_rust.ps1
#>

$ErrorActionPreference = "Stop"

$crateDir = $PSScriptRoot
$pyDir    = Join-Path $crateDir "..\Othello.Python" | Resolve-Path
$wheelDir = Join-Path $crateDir "target\wheels"

# cargo が PATH に無い場合に備え、rustup 既定の bin を先頭に補う
$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
if (Test-Path $cargoBin) { $env:Path = "$cargoBin;$env:Path" }

# maturin の実行ファイルを解決する（PATH 上 → pip --user の Scripts ディレクトリの順）
$maturin = (Get-Command maturin -ErrorAction SilentlyContinue).Source
if (-not $maturin) {
    $maturinCands = @(
        (Join-Path $env:APPDATA  "Python\Python314\Scripts\maturin.exe"),
        (Join-Path $env:APPDATA  "Python\Scripts\maturin.exe")
    )
    $maturin = $maturinCands | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $maturin) { throw "maturin が見つかりません。'py -m pip install --user maturin' を実行してください。" }

# ビルド対象の Python インタプリタを解決する（abi3 でも 1 つ必要）
$python = (& py -c "import sys; print(sys.executable)" 2>$null)
if (-not $python) { $python = (Get-Command python -ErrorAction SilentlyContinue).Source }
if (-not $python) { throw "Python インタプリタが見つかりません。" }

Write-Host "==> maturin: $maturin"
Write-Host "==> python : $python"
Write-Host "==> maturin build --release ($crateDir)"
& $maturin build --release --interpreter $python `
    --manifest-path (Join-Path $crateDir "Cargo.toml") --out $wheelDir
if ($LASTEXITCODE -ne 0) { throw "maturin build が失敗しました (exit $LASTEXITCODE)" }

# 最新のホイールを取得する
$wheel = Get-ChildItem $wheelDir -Filter *.whl -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $wheel) { throw "ホイールが生成されませんでした: $wheelDir" }
Write-Host "==> built wheel: $($wheel.Name)"

# ホイール(zip)から拡張モジュール(*.pyd / *.so)を取り出して Othello.Python へ配置する
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($wheel.FullName)
try {
    $entries = $zip.Entries | Where-Object {
        $_.Name -like "othello_ai_rust*" -and ($_.Name -like "*.pyd" -or $_.Name -like "*.so")
    }
    if (-not $entries) { throw "ホイール内に拡張モジュール(*.pyd/*.so)が見つかりません: $($wheel.Name)" }
    foreach ($e in $entries) {
        $target = Join-Path $pyDir $e.Name
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $true)
        Write-Host "placed: $target"
    }
}
finally {
    $zip.Dispose()
}

Write-Host "==> done. dotnet build で出力にコピーされ、実行時に Rust 実装が使われます。"
