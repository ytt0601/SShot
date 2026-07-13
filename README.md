# SShot

Windows向けスクリーンショットソフトウェア(C# / .NET 10, WPF)。

範囲選択・ウィンドウ・フルスクリーン(マルチモニタ対応)・スクロールキャプチャに対応し、
矢印/テキスト/図形/モザイク・ぼかしなどの編集機能、クリップボード/ローカル保存、
タスクトレイ常駐+グローバルホットキー、日本語/英語UIを備える。

アーキテクチャ上の決定や規約は [CLAUDE.md](./CLAUDE.md) を参照。

## 必要環境

- Windows 10/11
- .NET 10 SDK

## ビルド / 実行 / テスト

```powershell
dotnet build
dotnet run --project src/SShot.App
dotnet test
```

## 配布パッケージのビルド

```powershell
./build/publish-portable.ps1          # ポータブル単一exe -> build/publish/SShot.App.exe
dotnet build installer/wix/SShot.Installer.wixproj   # MSI -> installer/wix/bin/x64/Debug/SShot-Setup.msi
```

## プロジェクト構成

- `src/SShot.App` — WPF UI(View/ViewModel、トレイ、ホットキー)
- `src/SShot.Core` — キャプチャ/画像処理/アノテーション/設定などのロジック
- `tests/SShot.Core.Tests` — `SShot.Core` のユニットテスト(xUnit)
- `installer/` — WiXインストーラー定義
- `build/` — 発行用スクリプト
