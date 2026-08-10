# 18. 外部ライブラリ — 採用一覧・依存の鉄則・セットアップ

[← 前: 17_Tests](17_Tests.md) | [索引](00_Roadmap.md) | [次: 索引へ戻る →](00_Roadmap.md)

## この章で分かること

- Seed が採用している外部ライブラリ8種と、それぞれが担う役割
- **依存の鉄則**——どの層が外部ライブラリに依存してよいか（決定性を守る境界線）
- 新しい環境での初回セットアップ手順（UPM は自動・NuGet は1メニュー）
- 各ライブラリの「Seed での使い方」への入口（詳細は各章）
- 導入時に踏んだ罠と対処（NuGet の依存解決・MessagePack の鶏卵・DebugSheet のパッチ）

## 前提

- [02_Hub.md](02_Hub.md) の「合成ルート」、[12_GameCore.md](12_GameCore.md) の「決定性」を理解していること

この章が初出の主な用語: DI（依存性注入）/ UPM / NuGet / Source Generator / Tween。

## 1. 採用ライブラリ一覧

| ライブラリ | 役割 | 導入経路 | 主に使う場所 |
|---|---|---|---|
| **UniTask** 2.5.11 | ゼロアロケーションの async/await | UPM (git) | ロード・遷移・IO（`Seed.Flow` / `Seed.Assets`） |
| **VContainer** 1.19.0 | DI（生成と寿命の宣言） | UPM (git) | App の合成ルート（`Sample_GameFlowRunner`） |
| **LitMotion** 2.0.2 | Tween（コード制御・ゼロアロケ） | UPM (git) | UI 遷移・演出（`Sample_FadeTransition`） |
| **MasterMemory** 3.0.4 | 読み取り専用インメモリDB | NuGet | マスターデータのバイナリ（`Sample_MasterBinary`） |
| **MessagePack** 3.1.8 | バイナリシリアライザ | NuGet + UPM (git) | MasterMemory の土台 |
| **Addressables** 2.9.1 ＋ **Smart Addresser** 1.2.0 | アセット配信とアドレス自動付与 | UPM | `Seed.Assets`（IAssetLoader） |
| **ZLogger** 2.5.10 | ゼロアロケ構造化ログ | NuGet | `Seed.Logging`（GameLog） |
| **UnityDebugSheet** 1.5.4 | 実行中デバッグメニュー | 埋め込み（`Packages/`） | `Sample_DebugPage` |
| **NuGetForUnity** 4.5.0 | NuGet パッケージ管理 | UPM (git) | 上記 NuGet 系の導入手段 |

> 📖 **用語 — UPM / NuGet**: UPM は Unity 公式のパッケージ管理（`Packages/manifest.json` に列挙）。
> NuGet は .NET 汎用のパッケージ配布で、Unity では NuGetForUnity が DLL を
> `Assets/Packages/` へ展開します。Cysharp 系の純 .NET ライブラリは NuGet 経路になります。

## 2. 依存の鉄則（これだけは守る）

**外部ライブラリはすべて「殻」の道具**です。次の層は今後も外部ライブラリに依存させません。

| 層 | 外部依存 | 理由 |
|---|---|---|
| `Seed.Core`（GameCore） | **禁止** | 決定性の心臓部。await の再開フレームは非決定なので、async を1箇所でも持ち込むとリプレイが壊れる |
| `Seed.Hub.Contracts` | **禁止** | エンジン非依存の契約層（`noEngineReferences`）。ヘッドレス検証の要 |
| `Seed.Persistence` | **禁止** | 同上（純C#の封筒とファイルIOのみ） |
| その他の基盤 | **必要最小限** | `Seed.Flow`→UniTask、`Seed.Assets`→UniTask+Addressables のように用途が本質的な場合のみ |
| App 層 | 自由 | VContainer・LitMotion・DebugSheet はここだけ |

asmdef の `references` に書かなければコンパイラが強制してくれます。
「便利だから」で参照を追加する前に、この表に戻ってください。

> 📖 **用語 — DI（依存性注入）**: 部品が必要とする相手を、部品の外から渡す設計。Seed は元々
> 合成ルートで手渡し（手書きDI）しており、VContainer はその宣言を省力化する道具です。
> 通信（Hub）や駆動順（TickPipeline）の代替ではありません。

## 3. セットアップ（新しい環境の初回構築）

1. リポジトリを取得して Unity 6000.5 で開く——UPM パッケージ（UniTask/VContainer/LitMotion/
   Addressables/Smart Addresser/NuGetForUnity/MessagePack Unity 連携）は `Packages/manifest.json` から自動解決
2. NuGet 系が未復元の場合（`Assets/Packages/` に DLL が無い・コンパイルエラーが出る場合）は
   メニュー **`Seed/Setup/Install NuGet Packages`** を実行——依存解決込みで一括インストールされます
   （CI では `-executeMethod Seed.Tools.Editor.LibrarySetup.InstallFromCommandLine`）
3. 以後は NuGetForUnity の自動復元（`Assets/packages.config` 基準）で維持されます

採用 NuGet パッケージの台帳は `Assets/Script/Tools/Editor/LibrarySetup.cs` の `Packages` 配列です。
**バージョンは明示固定**（「最新を追う」と環境ごとに差が出るため）。追加時はここに1行足します。

## 4. 各ライブラリの Seed での使い方

### UniTask — 「殻」限定の非同期

- フェーズ遷移のロードは `UniTaskFlowOperation`（`Assets/Script/Flow/Runtime/UniTaskFlowOperation.cs`）で
  async 関数を `IFlowOperation` に包んで `CreateLoadOperation` から返す（→ [04_Flow.md](04_Flow.md)）
- アセットロードは `IAssetLoader` が `UniTask<T>` を返す（下記 Addressables）
- **規約: `async` を書いてよいのはロード・遷移・IO・UI 演出だけ。** GameCore・Behavior・
  Tick 系ロジックには持ち込まない（持ち込むとリプレイが壊れる）

### VContainer — 合成ルートの宣言化

- 永続ルート `Sample_GameFlowRunner` は `LifetimeScope` 継承になり、`Configure` が
  「何を作り誰に渡すか」の宣言、`Sample_GameLoop`（`IStartable`/`ITickable`/`ILateTickable`）が
  「どの順で初期化し毎フレーム何を回すか」を持つ（→ [01_Demo.md](01_Demo.md) の 4 節）
- **DI が置き換えたのは new の配線だけ**。Hub（通信）・TickPipeline（駆動順）・
  フェーズ内の CompositionScope（逆順片付け）はそのまま
- `IDisposable` 登録は逆順で自動 Dispose される（Clock→Flow の順に登録＝Flow が先に畳まれる）

### LitMotion — コード制御の Tween

- 画面遷移は `Sample_FadeTransition`（`IScreenTransition` 実装）が実例。
  `LMotion.Create(from, to, seconds).WithEase(...).Bind(値の適用先)` の1行で、
  インスペクター設定なしにコードから制御する（→ [06_UI.md](06_UI.md)）
- 使いどころは「艶」（UI・演出）だけ。Tick 側のロジック値には使わない

### ZLogger — ログ基盤（Seed.Logging）

```csharp
using Seed.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;

// 合成ルートで一度（Sample_GameLoop.Start が実行済み）
GameLog.InitializeForUnity();

// 使う側: カテゴリ付きロガーを作って ZLog 系で書く（補間はゼロアロケーション）
var logger = GameLog.CreateLogger("Battle");
logger.ZLogInformation($"敵を撃破: id={enemyId.Value}");
```

出口は差し替え可能——本番でファイルへ出すなら `GameLog.Initialize(独自のLoggerFactory)`。
未初期化でも Unity Console へ出る安全側の既定を持ちます。

### MasterMemory — マスターデータのバイナリ運用

- 入力（コード直書き・SO）→ メニュー **`Seed/Master Data Bake`** で
  `StreamingAssets/master.bytes` へ焼く → 実行時は自動でバイナリを読む
  （無ければコード直書きへフォールバック＝ベイク無しでも動く）
- 実装: `Sample_MasterTables.cs`（`[MemoryTable]` の行型）と `Sample_MasterBinary.cs`
  （Bake/Load）。**MasterDataSet の契約は不変**なので消費側は無変更（→ [07_Data.md](07_Data.md)）
- データを変えたらベイクし直す。戻すのは **`Seed/Master Data Bake 削除`**

### Addressables ＋ Smart Addresser — アセット配信

- 消費側は `Seed.Assets` の `IAssetLoader`（`LoadAsync<T>` / `InstantiateAsync` / `Release`）だけを知る。
  実装 `AddressablesAssetLoader` は合成ルートで注入する
- Addressables の初期設定: `Window > Asset Management > Addressables > Groups` で
  設定を作成 → アセットへアドレスを付与
- アドレス付与の人力運用は崩壊するので **Smart Addresser のルールで自動化**する:
  `Window > Smart Addresser > Layout Rule Editor` でルール資産を作り、
  「このフォルダ配下は自動でこのグループ・このアドレス」を宣言する
- 現状のデモはプリミティブ生成のためローダー未使用（基盤と契約だけ先行整備）。
  実アセットが入った時点で `StagePaletteAsset` のプレハブ直参照を AssetReference 化していく

### UnityDebugSheet — 実行中デバッグメニュー

- エディタで Play すると自動で立ち上がる（`Sample_DebugPage.TryAttach`）。
  ポーズ・倍速・ヒットストップ・視点切替を GUI から叩ける——**すべて Hub への命令発行**なので
  デバッグ専用の裏口は無く、トレーサにも記録される
- 実機ビルドで使う場合は `DebugSheetCanvas` プレハブをシーンへ置く

## 5. よくあるつまずき（導入時に実際に踏んだ罠）

- **症状**: NuGet の DLL が足りずコンパイルエラー → **原因**: NuGetForUnity の自動復元は
  「packages.config に依存の閉包が列挙済み」前提で、依存解決をしない →
  **対処**: `Seed/Setup/Install NuGet Packages`（正規の依存解決込み）を実行する
- **症状**: `com.github.messagepack-csharp`（Unity 連携）がコンパイルエラー →
  **原因**: NuGet 側の MessagePack 本体 DLL が先に必要（鶏と卵） →
  **対処**: 上記セットアップの順どおり（NuGet 復元 → UPM 解決の順で直る）
- **症状**: UnityDebugSheet がコンパイルエラー（`GetInstanceID` 非推奨） →
  **原因**: Unity 6000.5 で当該 API がエラー昇格。上流未対応 →
  **対処**: 対処済み。`Packages/com.harumak.unitydebugsheet` へ**埋め込み**、4箇所を
  `GetHashCode()`（インスタンスID由来で一意性同等）へパッチしてある。上流が対応したら
  埋め込みを削除して UPM (git) 指定に戻す
- **症状**: リプレイが一致しなくなった → **原因**: ロジック側に async/外部ライブラリの
  乱数・時刻が混入した疑い → **対処**: 2節の鉄則へ戻る。`Seed.Core` の asmdef 参照を確認する

## 6. 関連ファイルとテスト

- 台帳・セットアップ: `Assets/Script/Tools/Editor/LibrarySetup.cs` / `Packages/manifest.json` / `Assets/packages.config`
- 基盤: `Assets/Script/Logging/Runtime/GameLog.cs` / `Assets/Script/AssetLoad/Runtime/IAssetLoader.cs` /
  `Assets/Script/Flow/Runtime/UniTaskFlowOperation.cs`
- App 統合: `Sample_GameFlowRunner.cs`（LifetimeScope）/ `Sample_GameLoop.cs` /
  `Sample_FadeTransition.cs` / `Sample_DebugPage.cs` / `Samples/Master/`（テーブルとベイク）
- テスト: `Assets/Script/App/Tests/Editor/MasterBinaryTests.cs`（バイナリ往復・ベイクの決定性）

[← 前: 17_Tests](17_Tests.md) | [索引](00_Roadmap.md)
