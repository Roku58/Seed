# 07. Seed.Data — マスターデータ（数値・定義データの一元管理）

[← 前: 06_UI](06_UI.md) | [索引](README.md) | [次: 08_Character →](08_Character.md)

## この章で分かること

- マスターデータ（武器・ユニット・ステージなどの定義データ）を Seed でどう持つか
- 定義の入口2本立て——純C# DTO（`EntityDefinitionData`）と ScriptableObject（`EntityDefinitionAsset`）の使い分け
- 台帳 `MasterDataSet` に登録し、検証し、`MasterDataLoader` で Seed.Core へ流し込む3手の流れ
- エディタ支援ツール（`Seed/Master Data Browser`・`Seed/Master Data Validate`・共通 Inspector）の使い方
- ID の規約（1以上・型をまたいで全体一意・採番帯の運用）と、なぜそうなっているか

## 前提

先に読んでおくと理解が速い章:

- [01_Demo](01_Demo.md) — `Sample_GameFlowRunner` を使ったデモの起動手順（この章の「動かして試す」で使います）
- [02_Hub](02_Hub.md) — 契約（Contracts）の考え方。本章の `IEntityDefinition` も「最小の契約」の一例です
- [03_Clock](03_Clock.md) — 決定性の概念。本章では「登録順の決定性」として再登場します
- [04_Flow](04_Flow.md) — フェーズの概念。ホーム画面のメニュー行がマスターデータから組み立てられます

この章が初出の主な用語: マスターデータ、DTO、ScriptableObject、JsonUtility、CreateAssetMenu、CustomEditor、台帳、採番帯

> 📖 **用語 — マスターデータ**: ゲーム中に書き換わらない定義データの総称。武器の攻撃力、
> ステージの敵出現間隔、ユニットの移動速度など「企画が決める数値の表」。実行中に変化する
> セーブデータ（プレイヤーの所持金など）とは明確に区別します。

## 1. これは何か

Seed.Data は、マスターデータの**台帳・検証・Seed.Core への流し込み**を担う基盤です。

これが無いと何に困るかを具体的に挙げます。

- **数値がコードに散らばる**: 「敵の攻撃間隔 4 秒」がフェーズのコードに直書きされると、企画が調整するたびにプログラマの手が要ります。定義データとして一箇所に集めれば「データを1件足す・書き換える」だけで済みます。
- **ID の衝突に実行時まで気付けない**: 定義の ID はレコード・セーブ・リプレイにそのまま載る値です。重複や未設定を放置すると「セーブデータの ID 10 がどの定義を指すのか解釈がブレる」という再現困難な不具合になります。Seed.Data は登録時と検証時の2段階で早期に例外にします。
- **データ形式の変更が Core まで波及する**: JSON から ScriptableObject へ、あるいは CSV へと入口を変えるたびにゲームロジックが書き換わるのは避けたい。Seed.Data は入口を `IEntityDefinition` という最小契約に揃え、Core に触るのは `MasterDataLoader` だけに閉じます。

定義の入口は2本立てです。**純C# DTO**（`EntityDefinitionData`。JSON・コード直書き・テスト用）と、**ScriptableObject**（`EntityDefinitionAsset`。Inspector 編集用）。どちらも外へは `IEntityDefinition` しか見せないため、台帳から先の処理は完全に共通です。

> 📖 **用語 — DTO (Data Transfer Object)**: 振る舞いを持たず、データの入れ物に徹する純C#クラス。
> 本章の `EntityDefinitionData` は「ID と名前＋派生で足す数値フィールド」だけを持つ DTO 基底です。

> 📖 **用語 — ScriptableObject**: MonoBehaviour と違い GameObject に載せず、`.asset` ファイルとして
> プロジェクトに保存できる Unity のデータコンテナ。Inspector で編集でき、企画がデータを並べる
> 用途に向きます。略して SO と書きます。

## 2. 全体像

### 部品表

| 部品 | 種別 | 役割 |
| --- | --- | --- |
| `IEntityDefinition` | 純C# interface | `int Id` / `string DebugName` の最小契約 |
| `EntityDefinitionData` | 純C# DTO 基底 | JSON・コード・テスト用の入口 |
| `EntityDefinitionAsset` | abstract ScriptableObject | Inspector 編集用の入口 |
| `MasterDataSet` | 純C# | 型ごと×ID の台帳。追加・引き当て・検証 |
| `MasterDataLoader` | 純C# | Seed.Core への流し込み（出口） |
| `MasterDataBrowserWindow` | EditorWindow | 一覧・検索・検証・空きID提案 |
| `MasterDataInspection` | static クラス | アセット走査・検証の共通ロジック |
| `EntityDefinitionAssetInspector` | CustomEditor | SO 派生すべてに効く共通 Inspector |

### データの流れ

```
[入口]                                  [台帳]                [出口]              [Seed.Core]
純C# DTO（JSON / コード / テスト）──┐
                                    ├─→ MasterDataSet ──RegisterAll──→ EntityRegistry（ID→実体）
SO（EntityDefinitionAsset 派生）────┘    型ごと×ID索引
                                         ＋ID検証      ──BindFactories→ FactoryRegistry<int,TArg,TProduct>
                                                                         （ID→生成関数）
```

入口が何本増えても、台帳から先（`RegisterAll` / `BindFactories`）は一切変わりません。

> 📖 **用語 — EntityRegistry**: ID とゲーム内実体の対応表（詳細は [12_GameCore](12_GameCore.md)）。
> ID 0 は「対象なし」を表す予約値 `EntityRegistry.None` です。本章では「定義を ID のまま登録する
> 送り先」とだけ理解すれば十分です。

## 3. 動かして試す

### 3-1. デモのマスターデータを見る（コード直書きカタログ）

1. 新規シーンを作成（File > New Scene）
2. Hierarchy で右クリック → Create Empty で空の GameObject を作成
3. Inspector の Add Component から `Sample_GameFlowRunner` をアタッチ（カメラ・ライトは無ければ Start が自動生成します）
4. Play を押す
5. ホーム画面のメニュー「[1] 出撃: 草原（敵の攻撃: ゆっくり） / [2] 出撃: 火山（敵の攻撃: 速い） / [3] ショップ / [4] 出撃: 迷宮（自動生成・出口を探せ） / [5] 出撃: 市街（自動生成・店に寄れる）」を確認
   （画面に出るのは `DisplayName`。`Grassland` / `Volcano` / `Labyrinth` / `Township` は `DebugName` でログ・エディタ表示用です）

各行の表示名・地面色・敵攻撃間隔は、すべて `Sample_MasterCatalog.Build` が組み立てる `Sample_StageSpec` のマスターデータ由来です。ステージ ID は 201〜204（Grassland=201 / Volcano=202 / Labyrinth=203 / Township=204）、ユニットは 1〜99 の帯を使っています。

### 3-2. メニューから一括検証する

1. メニューバーの Seed > Master Data Validate をクリック
2. Console に `[MasterData] 検証OK（定義 N 件・ID重複なし）` が出ます。違反（ID 未設定=0 以下 / ID 重複）があれば1件ずつ `Debug.LogError` で報告されます

> 注意: この検証が走査するのは **ScriptableObject の定義（`EntityDefinitionAsset` 派生）のみ**です。
> デモのカタログ（`Sample_MasterCatalog`）はコード直書きの純C#なので対象外。さらに現状の
> プロジェクトには `EntityDefinitionAsset` の具象派生クラスが存在しないため、初期状態では
> 「定義 0 件」と表示されます。SO ルートを試すには次の 3-4 で派生クラスを自作します。

### 3-3. Master Data Browser を開く

1. メニューバーの Seed > Master Data Browser をクリック
2. 「Master Data」というタイトルのウィンドウ（最小サイズ 420×240）が開きます
3. ツールバーには「再走査」「検証」ボタン・検索フィールド・「定義 N 件」の件数表示が並びます
4. 一覧は全定義アセットを型を問わず ID 昇順で表示。`[ID]  名前    (型名)` の形式です
5. その下に「次の空きID: N　（100以降: N / 200以降: N）」の提案が出ます
6. 行をクリックするとそのアセットが選択・Ping され、そのまま Inspector で編集できます
7. ID 重複があると件数付きの赤い Error HelpBox が出て、該当行（と ID が 0 以下の行）が赤色表示になります

ウィンドウはフォーカスされるたびに自動で再走査するので、アセットを増減したら一度クリックすれば追従します。

### 3-4. SO 派生クラスを自作して共通 Inspector を体験する

1. Project ビューで `Assets/Script/App/Samples` を右クリック → Create > C# Script → 名前を `WeaponAsset` に（Seed.App アセンブリは Seed.Data を参照済みなのでこの場所ならすぐ使えます）
2. 作成したファイルをダブルクリックして開き、次の内容に置き換えて保存

```csharp
using UnityEngine;
using Seed.Data;

/// <summary>武器1種の定義アセット（SO ルートの練習用）。</summary>
[CreateAssetMenu(menuName = "Seed/Weapon Definition", fileName = "Weapon_New")]
public sealed class WeaponAsset : EntityDefinitionAsset
{
    /// <summary>攻撃力。</summary>
    [SerializeField] private int _attack;

    /// <summary>攻撃力（読み取り専用公開）。</summary>
    public int Attack => _attack;
}
```

3. コンパイル完了後、Project ビューで右クリック → Create > Seed > Weapon Definition
4. アセット名を `Weapon_Bite` などにして Enter
5. アセットを選択して Inspector を見ると、「Id」「Debug Name」「Attack」のフィールドに加えて、赤い HelpBox「ID が未設定です（0 は「対象なし」の予約値）。1以上を割り当ててください。」と「空きIDを割り当て（候補: 1）」ボタンが表示されます
6. ボタンをクリック → Id に空き番号が自動で入り、警告が消えます
7. Cmd+D（Windows は Ctrl+D）でアセットを複製すると ID が重複し、Inspector に「ID N は他の定義と重複しています」の赤 HelpBox、Master Data Browser では両方の行が赤色＋Error HelpBox になります

この共通 Inspector とブラウザは `EntityDefinitionAsset` 派生すべてに**無改修で**効きます。派生側は基底クラスの継承と `[CreateAssetMenu]` の付与だけです。

> 📖 **用語 — CreateAssetMenu**: ScriptableObject 派生クラスに付けると、Project ビューの
> Create メニューに「このアセットを作る」項目が生える Unity の属性。`menuName` がメニュー上の
> 表示位置、`fileName` が新規作成時の既定ファイル名です。

> 📖 **用語 — CustomEditor**: 特定の型の Inspector 描画を差し替える Unity エディタ拡張の仕組み。
> `EntityDefinitionAssetInspector` は `[CustomEditor(typeof(EntityDefinitionAsset), editorForChildClasses: true)]`
> と宣言されており、`editorForChildClasses: true` によって派生クラス全部に自動適用されます。

## 4. コードで使う

### 最小例（純C# DTO ルート）

```csharp
using Seed.Core;
using Seed.Data;

// 1) 定義型: EntityDefinitionData を継承してフィールドを足すだけ
public sealed class WeaponData : EntityDefinitionData
{
    public int attack; // JsonUtility 対応のため小文字始まり（本基盤唯一の命名例外）

    public WeaponData() { } // JSON 経由で作るなら既定コンストラクタが必要

    public WeaponData(int id, string name, int attack) : base(id, name)
    {
        this.attack = attack;
    }
}

// 2) 台帳へ登録 → 検証 → Seed.Core へ流し込み（合成ルートで1回だけ行う）
var data = new MasterDataSet();
data.Add(new WeaponData(100, "斬り上げ", 40));
data.Add(new WeaponData(102, "尻尾", 30));
data.ValidateGlobalIdUniqueness();                  // 型をまたぐ ID 重複を早期検出

var registry = new EntityRegistry();
new MasterDataLoader().RegisterAll(data, registry); // データが持つ ID のまま決定的順序で登録

// 3) 読み取り
var w = data.Get<WeaponData>(100);   // 未登録なら即 LogicException（不具合を隠さない）
var all = data.GetAll<WeaponData>(); // 常に ID 昇順の読み取り専用ビュー
for (var i = 0; i < all.Count; i++)  // ホットパスは for + インデクサ（foreach は列挙子を確保する）
{
    UnityEngine.Debug.Log(all[i].DebugName);
}
```

任意データ（無くてもよいデータ）の確認には `TryGet<TDefinition>(id, out var def)` と `Contains<TDefinition>()` を使います。`Get` は「無いのは構成ミス」という前提の API です。

### 実戦例（JSON 読み込み＋ID→振る舞いの紐付け）

```csharp
using System;
using Seed.Core;
using Seed.Data;
using UnityEngine;

// スキル定義（JSON から読む想定の DTO）
[Serializable]
public sealed class SkillData : EntityDefinitionData
{
    public int ratePermille; // 効果量（‰）
    public SkillData() { }
    public SkillData(int id, string name, int ratePermille) : base(id, name)
    {
        this.ratePermille = ratePermille;
    }
}

// スキル発動時に生成される振る舞い（例）
public sealed class SkillEffect
{
    public readonly int RatePermille;
    public SkillEffect(int ratePermille) { RatePermille = ratePermille; }
}

// --- 合成ルートでの組み立て ---
var data = new MasterDataSet();

// JSON テキストから定義を作る（フィールド名 = JSON キー名。小文字始まりはこのため）
var json = "{\"id\":300,\"debugName\":\"攻撃強化\",\"ratePermille\":150}";
data.Add(JsonUtility.FromJson<SkillData>(json));
data.ValidateGlobalIdUniqueness();

var registry = new EntityRegistry();
var loader = new MasterDataLoader();          // バッファ再利用のためインスタンス型。1つ作って使い回す
loader.RegisterAll(data, registry);

// 定義 ID → 生成関数を FactoryRegistry へ紐付ける（型引数3つは明示必須）
var factories = new FactoryRegistry<int, int, SkillEffect>(); // TArg=レベル の例
loader.BindFactories<SkillData, int, SkillEffect>(
    data, factories, (def, level) => new SkillEffect(def.ratePermille * level));

// レコード・セーブに載っている ID から、その場で振る舞いを生成できる
if (factories.TryCreate(300, 5, out var effect))
{
    Debug.Log(effect.RatePermille); // 750
}
```

> 📖 **用語 — JsonUtility**: Unity 標準の JSON 直列化 API。「フィールド名＝JSON のキー名」で
> 対応付け、引数付きコンストラクタは呼べません。`EntityDefinitionData` の公開フィールドが
> 小文字始まり（`id` / `debugName`）なのは JSON の可読性を優先した本基盤唯一の命名例外で、
> 他の場所へ真似して広げないでください。

> 📖 **用語 — FactoryRegistry**: 「キー → 生成関数」の対応表（`FactoryRegistry<TKey, TArg, TProduct>`。
> 詳細は [12_GameCore](12_GameCore.md)）。`BindFactories` はキー型を int（定義 ID）に固定して
> 受け取ります。レコードに載るのは ID だけなので、追加の対応表なしに「ID → 振る舞い」を辿れます。

## 5. 仕組み

### 台帳の内部構造

`MasterDataSet` は「定義の型ごと」に保管箱（Bucket）を持ち、各箱が「ID→定義の辞書（O(1) 引き当て）」と「ID 昇順リスト」の両方を保持します。昇順リストは挿入時に二分探索で位置を決めるため、`Add` の呼び出し順に関係なく常に同じ並びになります。`GetAll` が返す型付きビューは箱ごとに1回だけ確保してキャッシュされ、以後の呼び出しで GC 確保はありません。

型キーは**完全一致**で、継承関係は辿りません。基底型で辿れるようにすると「複数の派生型に同じ ID が居る」場合の解が曖昧になり、探索も O(1) でなくなるためです。どの単位で束ねるかは `Add<TDefinition>` の型引数で呼び出し側が明示する規約です。唯一の例外が `AddByRuntimeType` で、Inspector に並べた `EntityDefinitionAsset[]` のような「基底型の1本の配列に複数種類が混在する」入口だけ、実行時型で束ねます。

### 流し込みの決定性

`MasterDataLoader.RegisterAll` は次の順で動きます。

1. `ValidateGlobalIdUniqueness` — 型をまたいだ ID 重複を検証（違反なら登録 0 件のまま、原因の型名・DebugName 入りの例外で落ちる）
2. `CollectAllOrderedById` — 全型の定義を再利用バッファに集め、ID 昇順にソート
3. `EntityRegistry.RegisterWithId(definition, definition.Id)` を昇順に呼ぶ
4. バッファを Clear（ScriptableObject への参照を残さないため）

辞書の列挙順は保証されないため、そのまま流すと「実行ごとに登録順が変わる」ことになります。登録順に意味は無くても、順序が揺れるとトレースログの差分比較やバグ再現が壊れます。だから ID 昇順に固定します——これが [03_Clock](03_Clock.md) で学んだ決定性の、データロードにおける現れ方です。同じ `MasterDataSet` を二度 `RegisterAll` しても、同一エンティティ＋同一 ID なら無害です（冪等）。

### 三大規約との関係

- **状態は Tick、艶は Update**: マスターデータは Tick 側のロジックが参照する「書き換わらない真実」です。だからこそ台帳は UnityEngine 非依存の純C#で、EditMode テストで全規約を検証できます（UnityEngine に依存するのは `EntityDefinitionAsset` 1ファイルだけ）。
- **方針は App**: 「何を何番で登録するか」はアプリの方針なので、カタログ（`Sample_MasterCatalog`）は Seed.App にあります。Seed.Data は台帳と規約だけを提供します。
- **命令の処理者は1基盤**: Seed.Data 自体は命令を処理しませんが、同じ思想で「Core のレジストリへ書き込む役は `MasterDataLoader` の1本に閉じる」構造です。入口（SO / JSON / コード）が何本増えても Core は変わりません。

asmdef 構成もこの分離を強制します: `Seed.Data`（Seed.Core のみ参照）/ `Seed.Data.Editor` / `Seed.Data.Editor.Tests` の3本です。

> 📖 **用語 — asmdef (Assembly Definition)**: コードを独立したアセンブリに分けて参照方向を
> 制限する Unity の仕組み。ゲーム側アセンブリから Seed.Data を使うには asmdef の参照追加が
> 必要です（Seed.App は追加済み）。

## 6. よくあるつまずき

- **症状**: `Add` で「定義 X のIDが不正: 0（1以上。0 は EntityRegistry.None の予約値）」
  **原因**: ID 未設定（0 は「対象なし」の予約値）。 **対処**: 1 以上を採番する。SO なら Inspector の「空きIDを割り当て」ボタンが使えます。
- **症状**: 「ID N が型をまたいで重複: X と Y」
  **原因**: 型が違っても ID は全体で一意という規約（EntityRegistry の ID 空間は全エンティティで1本）の破り。 **対処**: 「武器は 1000 番台」のような採番帯を型ごとに分ける（デモの実例: ユニット 1〜99／ステージ 201〜）。
- **症状**: `GetAll<派生型>()` が空を返す
  **原因**: `Add<基底型>` で登録した（型キーは完全一致で継承を辿らない）。 **対処**: `Add` の型引数を具体型に直すか、混在配列の入口なら `AddByRuntimeType` を使う。
- **症状**: `Get` で「マスターデータに未登録: X#N」
  **原因**: カタログ/JSON の取り込み漏れか ID の打ち間違い。 **対処**: 「無ければ既定値」は不具合を隠すので不採用の設計です。任意データだけ `TryGet` / `Contains` を使います。
- **症状**: `BindFactories` で「X の定義が0件（カタログ/JSONの取り込み漏れ）」
  **原因**: 対象型の定義が1件も台帳に無い。 **対処**: 取り込みを確認する。その仕様をタイトルから外す意図なら `BindFactories` 自体を呼ばないこと。二重バインドも既定で例外です（タイトル別差し替えの意図があるときだけ `allowOverwrite: true`）。
- **症状**: Master Data Browser を開いても 0 件
  **原因**: ブラウザが走査するのは `EntityDefinitionAsset`（`t:` 検索）のみで、純C#のコード直書きカタログは対象外。しかも現状プロジェクトに具象派生クラスは存在しない。 **対処**: 3-4 の手順で派生クラスを自作する。
- **症状**: `JsonUtility.FromJson` の結果が既定値のまま
  **原因**: 既定コンストラクタが無い、またはフィールド名と JSON キー名の不一致。 **対処**: 既定 ctor を用意し、フィールド名を JSON キーに合わせる（`id` / `debugName` が小文字始まりなのはこのため）。

## 7. 増やす・拡張する

- **純C#のデータ種を増やす**: `EntityDefinitionData` を継承してフィールドを足す（`Assets/Script/Data/Tests/Editor/MasterDataTests.cs` の WeaponDef が実例）。または `Sample_UnitSpec` / `Sample_StageSpec` のように `IEntityDefinition` を直接実装しても構いません。
- **SO のデータ種を増やす**: `EntityDefinitionAsset` を継承して `[CreateAssetMenu]` を付ける（3-4 の手順）。共通 Inspector とブラウザは無改修でそのまま効きます。
- **デモにステージを増やす**: `Assets/Script/App/Samples/Sample_MasterCatalog.cs` の `Build` に `catalog.Add(new Sample_StageSpec(...))` を1件足す（ID 帯規約: 1〜99=ユニット / 201〜=ステージ）。さらに `Sample_HomePhase` にメニュー行と入力分岐を追加します。
- **新しい入口形式（CSV・アドレサブル等）**: 「形式 → `IEntityDefinition` 実装 → `MasterDataSet.Add`」の変換を Seed.Data に足すだけです。台帳から先と Seed.Core は無変更で済みます。
- **ID→振る舞いの紐付けを増やす**: `BindFactories<TDefinition, TArg, TProduct>(data, factories, (def, arg) => ...)`。型引数は3つとも明示が必要です（ラムダの引数型から TDefinition を推論できないため）。
- **エディタ検証規則を増やす**: `Assets/Script/Data/Editor/MasterDataInspection.cs` の `Validate` に追加します（ブラウザの検証ボタンと `Seed/Master Data Validate` が共用）。ただし **Inspector の赤 HelpBox は `EntityDefinitionAssetInspector` 側に独自実装がある**ため、Inspector にも出したい規則はそちらへも追加してください。

## 8. 関連ファイルとテスト

- `Assets/Script/Data/Runtime/IEntityDefinition.cs` — 最小契約（`int Id` / `string DebugName`）
- `Assets/Script/Data/Runtime/EntityDefinitionData.cs` — 純C# DTO 基底
- `Assets/Script/Data/Runtime/EntityDefinitionAsset.cs` — ScriptableObject 基底
- `Assets/Script/Data/Runtime/MasterDataSet.cs` — 台帳と検証
- `Assets/Script/Data/Runtime/MasterDataLoader.cs` — Seed.Core への流し込み
- `Assets/Script/Data/Editor/MasterDataBrowserWindow.cs` — ブラウザ＋ Validate メニュー
- `Assets/Script/Data/Editor/MasterDataInspection.cs` — 走査・検証の共通ロジック
- `Assets/Script/Data/Editor/EntityDefinitionAssetInspector.cs` — 共通 Inspector
- `Assets/Script/App/Samples/Sample_MasterCatalog.cs` / `Sample_UnitSpec.cs` / `Sample_StageSpec.cs` — デモのカタログ実例

テスト: `Assets/Script/Data/Tests/Editor/MasterDataTests.cs`（EditMode 4件。Window > General > Test Runner の EditMode タブから実行）
— Catalog_AddGetGetAll_Works / ValidateGlobalIdUniqueness_DetectsCrossTypeDuplicates / RegisterAll_PutsDefinitionsIntoRegistryWithStableIds / RegisterAll_RejectsDuplicateIds_BeforeTouchingRegistry

[← 前: 06_UI](06_UI.md) | [索引](README.md) | [次: 08_Character →](08_Character.md)
