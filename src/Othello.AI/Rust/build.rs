// ビルドスクリプト: Windows ビルド時に生成バイナリ（othello_ai_rust.pyd）へ
// VERSIONINFO リソースを埋め込む。エクスプローラーのプロパティ「詳細」タブで
// バージョン・製品名などを確認できるようにする。
//
// 非 Windows ターゲットでは何もしない（winresource は cfg(windows) 限定の依存）。

fn main() {
    #[cfg(windows)]
    {
        let mut res = winresource::WindowsResource::new();
        // FileVersion / ProductVersion は CARGO_PKG_VERSION から自動設定される（1.0.0 → 1.0.0.0）。
        res.set("ProductName", "Othello");
        res.set("FileDescription", "Othello AI (Rust + PyO3)");
        res.set("LegalCopyright", "Copyright (c) 2026 sadaaki");
        // 会社名は空欄（C# 側のメタデータ方針に合わせる）。
        res.set("CompanyName", "");
        // .pyd は内部名と一致させておく。
        res.set("InternalName", "othello_ai_rust.pyd");
        res.set("OriginalFilename", "othello_ai_rust.pyd");

        if let Err(e) = res.compile() {
            // rc.exe / llvm-rc が見つからない環境ではリソース埋め込みをスキップし、
            // バイナリ自体のビルドは継続させる（警告のみ）。
            println!("cargo:warning=バージョンリソースの埋め込みに失敗しました: {e}");
        }
    }
}
