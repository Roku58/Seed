# 13. Seed.Data — マスターデータと入力補助（エディタ）

マスターデータの台帳・検証・Seed.Core への流し込み。定義の入口は2本立て:
純C# DTO（`EntityDefinitionData`。JSON/コード/テスト用）と
ScriptableObject（`EntityDefinitionAsset`。Inspector 編集用）。

## エディタメニューで試す

- **`Seed/Master Data Browser`**: 全定義アセット（EntityDefinitionAsset 派生）を型を問わず
  ID 昇順で一覧。検索（ID・名前・型名）／「再走査」「検証」ボタン／ID 重複・未設定の行は赤表示
  ／**次の空き ID を提案**／行クリックでアセットを Ping → そのまま Inspector で編集
- **`Seed/Master Data Validate`**: ウィンドウ無しの一括検証。
  Console に「[MasterData] 検証OK（定義 N 件・ID重複なし）」または違反ごとの LogError
- **共通 Inspector**: EntityDefinitionAsset 派生すべてに自動で効く。ID 未設定/重複を
  赤 HelpBox で警告し「空きIDを割り当て」ボタンで採番できる

> 注意: ブラウザが走査するのは **ScriptableObject の定義のみ**。デモのカタログ
> （`Sample_MasterCatalog`）はコード直書きの純C#なのでブラウザには出ない。
> また現状プロジェクトに EntityDefinitionAsset の具象派生クラスは無いため、
> SO ルートを試すには下記のような派生クラスの自作が必要。

## 最小コード

```csharp
using Seed.Core;
using Seed.Data;

// 1) 定義型: DTO 基底を継承してフィールドを足すだけ
public sealed class WeaponData : EntityDefinitionData
{
    public int attack; // JsonUtility 対応のため小文字始まり（本基盤唯一の命名例外）
    public WeaponData(int id, string name, int attack) : base(id, name) { this.attack = attack; }
}

// 2) 台帳へ登録 → 検証 → Seed.Core へ流し込み（合成ルートで1回）
var data = new MasterDataSet();
data.Add(new WeaponData(100, "斬り上げ", 40));
data.ValidateGlobalIdUniqueness();                  // 型をまたぐ ID 重複を早期検出

var registry = new EntityRegistry();
new MasterDataLoader().RegisterAll(data, registry); // データが持つ ID のまま決定的順序で登録

var w = data.Get<WeaponData>(100);   // 未登録なら即 LogicException（不具合を隠さない）
var all = data.GetAll<WeaponData>(); // 常に ID 昇順の読み取り専用ビュー
```

SO で作る場合: `EntityDefinitionAsset` を継承し `[CreateAssetMenu]` を付ける →
Project ビューでアセット作成 → 共通 Inspector とブラウザが無改修で効く。

## ハマりどころ

- ID は 1 以上必須（0 は None 予約）。**型が違っても全体で一意**
  （「武器は1000番台」等の採番帯運用が前提。デモ: ユニット1〜99／ステージ201〜）
- 型キーは完全一致で継承を辿らない（混在配列だけ `AddByRuntimeType`）
- `BindFactories` は定義0件で例外（取り込み漏れ検出）。二重バインドも既定で例外
- DTO の公開フィールド小文字始まりは JsonUtility 都合の意図的例外。他へ広げない
- ホットパスの GetAll は for + インデクサ（foreach は列挙子を確保する）

## 増やすとき

- データ種追加 = DTO 継承 or `IEntityDefinition` 直接実装（`Sample_StageSpec` が実例）
- デモにステージ追加 = `Sample_MasterCatalog.Build` に Add 1件＋ホームにメニュー行
- CSV・アドレサブル等の入口 = 「形式→IEntityDefinition→MasterDataSet.Add」の変換を足すだけ
- 検証規則の追加 = `MasterDataInspection.Validate`（ブラウザの検証ボタンと
  `Seed/Master Data Validate` が共用。Inspector の赤 HelpBox は
  `EntityDefinitionAssetInspector` 側に独自実装があるため、Inspector にも出したい規則はそちらへも追加）

主要ファイル: `Assets/Script/Data/Runtime/MasterDataSet.cs` / `MasterDataLoader.cs` /
`Assets/Script/Data/Editor/MasterDataBrowserWindow.cs`。
テスト: `Assets/Script/Data/Tests/Editor/MasterDataTests.cs`
