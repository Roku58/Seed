# 11. Seed.StageGen — ステージ自動生成（迷路・街・配置）

[← 前: 10_AI](10_AI.md) | [索引](00_Roadmap.md) | [次: 12_GameCore →](12_GameCore.md)

## この章で分かること

- パスの列を積むだけで迷路・街を作る仕組み（`GenerationPipeline` と `IGenerationPass`）
- 「設計図（`StageBlueprint`）」と「施工（`StageBuilder`）」を分ける理由——なぜ施工では一切抽選しないのか
- 同じシードなら地形も配置も**見た目のバリアントまで**同一になる決定性の作り方（パスごとの乱数 Fork）
- 穴掘り法・BSP 区画割り・BFS 距離帯バイオーム・braid（行き止まりの貫通）の各アルゴリズムの中身
- 美術アセットが 1 つも無くても必ず建つフォールバック連鎖と、アセットが届いたときの差し替え方

## 前提

先に次の章を読んでください。この章は各章の知識を使います。

- [04_Flow](04_Flow.md) — **フェーズ**の入場・退場。ステージの生成と施工はフェーズ入場時に 1 回だけ走り、退場時に親 GameObject ごと破棄されます
- [07_Data](07_Data.md) — **マスターデータ**と ID 運用。「ステージを増やす = `Sample_StageSpec` を 1 件足す」の作法、ステージ ID は 201 番から
- [08_Character](08_Character.md) — **ActorPose** と **IMotionSolver**。生成した壁が実際に移動を遮るのは、ユニットにカプセルコライダー＋Rigidbody の移動モーター（サンプルの `Sample_KinematicMotor`）を装着したときだけです

乱数 `DeterministicRandom`（xorshift32・`Seed.Core` 名前空間）はロジック基盤の道具で、詳細は [12_GameCore](12_GameCore.md) で扱います。本章では「同じシードなら同じ列が出る」「`Fork()` で独立した子ストリームを切り出せる」の 2 点だけ使います。

この章が初出の主な用語: プロシージャル生成、生成パス、設計図と施工、穴掘り法、braid、BFS、BSP、連結成分ラベリング、乱数の Fork、フォールバック連鎖、デリゲート。

## 1. これは何か

Seed.StageGen は「ステージの地形・区画・配置物を、シードから決定的に組み立てる純C#基盤」です。

> 📖 **用語 — プロシージャル生成**: マップやアイテムを人が 1 つずつ手で作る代わりに、規則と乱数で自動生成する手法。手作りの density（作り込み）を捨てずに面数を増やすため、実務では「自動生成した骨格に手作りの部屋を混ぜる」構成をとります。本基盤の `TemplateRoomsPass` がその混ぜ役です。

無いと困ることは 3 つあります。

1. **ステージ追加のコストがシーン作成に張り付く**。手作りだけだと 1 面ごとに Unity シーンを作り、床・壁・敵・出口を人手で並べることになります。本基盤ではステージ追加が「マスターデータ 1 件（`generatorKind` / `genWidth` / `genHeight` / `braidPermille`）」に縮みます。
2. **素朴な自動生成はリプレイ・セーブ・テストと両立しない**。`UnityEngine.Random` で床を並べると、同じステージへ再入場したときに地形が変わります。すると「入力列を再生すれば同じ試合が再現される」というロジック基盤の前提が壊れ、EditMode テストで地形の性質（全域連結など）を検証することもできません。本基盤は乱数源を `DeterministicRandom` 1 本に絞り、シードをステージ ID から決めることで、地形をリプレイの一部にしています。
3. **美術アセットの完成待ちで開発が止まる**。プレハブが無ければ床すら建たない設計だと、ゲームループの実装がアート待ちになります。本基盤は登録が無い組み合わせを内蔵プリミティブ（色つきキューブ）で建てるため、**アセット 0 個の状態でも遊べる**状態から始めて、届いたぶんだけ `Bind` 行を差し替えられます。

> 📖 **用語 — 決定性**（本プロジェクト固有の言い回し）: 「同じ入力から常にバイト単位で同じ出力が出る」性質。Seed ではロジック・生成の両方に課しており、リプレイ・セーブデータ・自動テスト・将来のロックステップ通信がすべてこの性質の上に乗っています。破る典型は `UnityEngine.Random`、`DateTime.Now`、`object.GetHashCode()`、`Dictionary` の列挙順への依存です。

## 2. 全体像

### 部品表

| 部品 | 役割 |
|---|---|
| `GenerationPipeline` | パス列を登録順に実行し `StageBlueprint` を返す。パスごとに乱数を `Fork()` |
| `StageBlueprint` | 設計図（純データ）。`Grid` / `Regions` / `Placements` / `CellSize` / `Seed` を持つ |
| `StageGrid` | 地形グリッド 3 レイヤー（セル種別・素材バリアント番号・バイオームID）＋ BFS 距離マップ |
| `CellType` / `BiomeId` / `PlacementKind` | 増える語彙の値型（`int` 値。標準は 1〜、アプリ独自は 100 以降を発番） |
| `GridRect` | グリッド上の矩形（区画・テンプレート範囲）。`UnityEngine.RectInt` を使わず純C#で持つ |
| `Placement` | 配置物 1 件（`Kind` / `RefId` / `X` / `Y`）。`RefId` の意味はアプリが決める |
| `IGenerationPass` | 生成パス 1 工程の契約（`Name` と `Execute(StageBlueprint, DeterministicRandom)`） |
| `RoomTemplate` / `RoomTemplateLegend` | 手作り部屋を文字列＋凡例で書く仕組み（配置物を内包できる） |
| `TileVariantTable` | (バイオーム, セル種別) → バリアント番号の重み表 |
| `PlacementTable` | 出現テーブル（`RefId` ＋重みの行。敵の種類・宝箱の中身の抽選） |
| `StageAssetPalette` / `TileFactory` | 施工側の素材登録表（(バイオーム, セル, バリアント) → タイル工場） |
| `PrimitiveTiles` | 内蔵プリミティブ工場（`FlatTile` / `BlockTile` / `MarkerTile`） |
| `StageBuilder` / `PlacementFactory` | 施工者。地形を建て、配置物を登録表どおりに実体化する（未登録は警告） |

### パス一覧（`.Add()` した順に実行される）

| パス | コンストラクタ引数の意味 | 何をするか |
|---|---|---|
| `FillPass(CellType cell)` | cell=埋める種別 | 全セルを 1 種別で塗る（各生成の下地） |
| `MazeCarvePass(int braidPermille = 0)` | 行き止まりを貫通させる確率‰（0=完全迷路） | 穴掘り法で迷路を掘り、braid で閉路を作る |
| `TemplateRoomsPass(IReadOnlyList<RoomTemplate> templates, int attemptsPerTemplate = 20)` | templates=スタンプ順、attempts=1 枚あたりの配置試行上限 | 手作り部屋を重ならない位置へスタンプし、分断を修復 |
| `BspDistrictPass(int minDistrictSize = 7)` | 区画の最小辺長（3 未満は 3 に丸める） | 再帰 2 分割で街区を割り、分割線を道路にする |
| `BuildingPlacementPass()` | 引数なし（建物の最小辺は 2 固定） | 各区画の内側に建物ブロックと出入口を置く |
| `BiomeAssignPass.DistanceBands(params Band[] bands)` | `Band(上限‰, BiomeId)` を近い順に並べる | 起点からの BFS 距離の割合でバイオーム帯を塗る |
| `BiomeAssignPass.RegionBased(params BiomeId[] candidates)` | 区画ごとに等確率抽選する候補（区画外は先頭の候補） | 区画単位でバイオームを塗る |
| `TileVariantPass(TileVariantTable table)` | 重み表（null なら空表＝全バリアント 0） | セルごとにバリアント番号を抽選し設計図へ焼く |
| `PlayerSpawnPass()` | 引数なし | 走査順で最初の歩行可能セルを開始地点にする |
| `EnemyPlacementPass(int count, int bandMinPermille, int bandMaxPermille, PlacementTable defaultTable, Dictionary<int, PlacementTable> biomeTables = null)` | count=体数、band…=距離帯の下限‰と上限‰、defaultTable=既定の出現テーブル、biomeTables=バイオーム別テーブル（バイオームID→表） | 距離帯に入る空きセルから抽選し、テーブルで種類を決めて置く |
| `LandmarkPlacementPass(PlacementKind kind, int refId, LandmarkRule rule, BiomeId regionBiomeFilter = default)` | kind=置く種別、refId=参照ID、rule=`Farthest`/`RandomWalkable`/`RegionCenter`、filter=`RegionCenter` 時に区画をバイオームで絞る | 意味オブジェクトを規則どおり 1 つ置く |

`EnemyPlacementPass` の引数はこの並びが正です（**体数 → 下限‰ → 上限‰ → 既定テーブル → バイオーム別テーブル**）。デモの実引数は迷宮が `new EnemyPlacementPass(1, 500, 1000, new PlacementTable().Add(敵ID, 1000))`、市街が `new EnemyPlacementPass(1, 400, 1000, ...)` です。

### データの流れ

```
Sample_StageSpec（マスターデータ: generatorKind / genWidth / genHeight / braidPermille）
   │ seed = 700 + StageId（迷宮 203 → 903 / 市街 204 → 904）
   ▼
GenerationPipeline.Generate(width, height, cellSize, seed)
   │  new DeterministicRandom(seed) ─ Fork() ─▶ パス1専用の乱数ストリーム
   │                                ─ Fork() ─▶ パス2専用の乱数ストリーム …
   ├─ FillPass → MazeCarvePass → TemplateRoomsPass → PlayerSpawnPass
   │  → BiomeAssignPass → TileVariantPass → EnemyPlacementPass → LandmarkPlacementPass
   ▼
StageBlueprint（純データ。抽選はここで完了している）
   ├─ Grid     : セル種別 / バリアント番号 / バイオームID の3レイヤー
   ├─ Regions  : GridRect の列（区画・スタンプ済みテンプレの範囲）
   └─ Placements: (Kind, RefId, X, Y) の列
   ▼
StageBuilder.Build(blueprint, parent)   ← ここでは一切抽選しない
   ├─ 地形: StageAssetPalette.Resolve(バイオーム, セル, バリアント) → TileFactory を呼ぶ
   └─ 配置: SetPlacement の登録表 → PlacementFactory を呼ぶ（未登録は Debug.LogWarning）
   ▼
parent Transform 配下の GameObject 群（フェーズ退場時に親ごと Destroy）
   └─ Placements の座標は GridToWorld() を通ってユニットの初期 Pose にもなる
```

## 3. 動かして試す

`SampleScene` には統合デモの入口が置かれていないため、手で 1 つ置きます。

1. Unity で任意のシーンを開く（`Assets/Scenes/SampleScene.unity` でも新規シーンでも可）
2. メニューの **GameObject → Create Empty** で空の GameObject を作る
3. Inspector の **Add Component** で `Sample_GameFlowRunner` を追加（設定項目はありません。カメラ・ライトは無ければ自動生成されます）
4. **Play** を押す。画面左上にホームメニューのテキストパネルが出る
5. **[4] キー**（出撃: 迷宮）を押す。21×21・シード 903 の迷路が生成されます
   - **Hierarchy**: `Stage_Labyrinth` という GameObject ができ、その下に 441 個（21×21）のタイル Cube と、青カプセル（プレイヤー）・灰キューブ（敵）が並ぶ
   - **見た目**: 入口側の床が緑（草原バイオームの 2 バリアント）、最奥側の床が赤茶（溶岩バイオームの 2 バリアント）、壁は灰色の高さ 2 ブロック（バイオーム不問の 2 バリアント）
   - **マーカー**: 緑＝出口（開始地点からの BFS 最遠点）、紫＝宝箱 3 個（宝物庫テンプレートに内包された 2 個＋乱択の 1 個）
   - **カメラ**: ステージ寸法（`max(幅, 高さ) × 1.5`）に合わせた俯瞰へ自動移動
   - **Console**: 正常時は何も出ません
6. **[W][A][S][D]** で移動する。壁に当たって止まる（生成ステージのユニットにはカプセルコライダー＋自前の移動モーターが装着されているため）
7. **紫マーカー**を踏む → 鬼人薬（攻撃 +15・20000ms）が発動し、そのマーカーだけ消える（1 回限り）
8. **緑マーカー**を踏む → ホーム画面へ帰還
9. **[5] キー**（出撃: 市街）を押す。31×31・シード 904 の街が生成されます
   - 全面が床の上に道路の格子が走り、区画ごとに建物ブロックが建つ。住宅街の区画は茶系、市場の区画は青系（区画バイオームの抽選結果）
   - **黄マーカー**＝ショップ（市場バイオームの区画中心近傍）。踏むとショップフェーズへ遷移する
10. ホームへ戻って同じステージへ再出撃する。**地形・敵の位置・マーカーの位置がすべて前回と同一**（決定性の確認）

Console に出る可能性があるのは 2 種類です。`SetPlacement` を登録していない配置種別が設計図に載っていると `[StageBuilder] 実体化表に無い配置種別: Placement#N（SetPlacement を追加すること）` という警告が出ます。パレットにも内蔵色にも無い未知のセル種別は**マゼンタ色**で建つので、目で見て気づけます。

### エディタでアセットを紐付ける（Seed/Stage Palette）

プレハブと役割（バイオーム・セル種別・バリアント・配置種別）の対応は、コードではなく
資産（ScriptableObject）に保存できます。手順は次のとおりです。

1. Unity メニューバーの **`Seed`** → **`Stage Palette`** をクリック（ウィンドウが開きます）
2. ツールバーの **「新規作成」** を押し、保存先とファイル名を決める（既定 `StagePalette.asset`）。
   Project ビューから `Seed > Stage Palette` で作っても同じものができます
3. **「定番の役割の雛形を並べる（未登録ぶんだけ行を追加）」** を押す。
   床・壁・道路・建物・出入口と、開始地点・敵の出現・出口・店・宝箱・イベントの行が並びます
4. 各行の **「プレハブ」** 欄へ Project ビューからプレハブをドラッグする。
   原点が足元でないモデルは **「高さ調整」** で補正し、1×1 前提のモデルは
   **「セルの大きさに合わせる」** をチェックする
5. 上部に出る案内を確認する
   - 「プレハブ未登録の地形（内蔵プリミティブで建ちます）: …」——**埋めていない役割があっても動きます**
   - 「不備 N 件」——キーの重複やプレハブ未設定。ツールバーの **「検証」** で Console にも出せます
6. **「保存」**（または `Ctrl+S`）

> 📖 **用語 — キーの重複が危険な理由**: パレットの登録は「同じ (バイオーム, セル種別, バリアント) は
> 後の行が上書きする」辞書です。重複した行は**黙って無効になる**ため、実行時には
> 「なぜかこのプレハブが使われない」という形でしか気づけません。
> ウィンドウの検証はこれを Play 前に見つけます。

> 📖 **用語 — なぜ素の ScriptableObject なのか**: マスターデータ基盤の
> `EntityDefinitionAsset`（→ [07_Data.md](07_Data.md)）を継承すると、StageGen が `Seed.Data` へ
> 依存することになり「StageGen は `Seed.Core` のみに依存」という構成が壊れます。
> ID 管理を必要としない紐付け表なので、素の ScriptableObject にしています。

## 4. コードで使う

### 最小例 — 迷路を生成して建てるだけ

```csharp
using Seed.StageGen;
using UnityEngine;

public sealed class MiniStageDemo : MonoBehaviour
{
    private void Start()
    {
        // 生成: パス列 → 設計図（同じ seed なら毎回まったく同じ迷路）
        var blueprint = new GenerationPipeline()
            .Add(new FillPass(CellType.Wall))                 // 全面を壁で下地塗り
            .Add(new MazeCarvePass(braidPermille: 200))       // 穴掘り＋2割の行き止まりを貫通
            .Add(new PlayerSpawnPass())                       // 開始地点（以降の距離計算の起点）
            .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
            .Generate(width: 21, height: 21, cellSize: 1.5f, seed: 903);

        // 施工: パレット未登録でも内蔵プリミティブで必ず建つ
        var builder = new StageBuilder()
            // 施工不要の種別も空実装で登録する（未登録だと警告が出る規約）
            .SetPlacement(PlacementKind.PlayerSpawn, (in Placement _, Vector3 __, Transform ___) => { })
            .SetPlacement(PlacementKind.Exit, (in Placement _, Vector3 pos, Transform parent) =>
                PrimitiveTiles.MarkerTile(new Color(0.2f, 0.9f, 0.4f))(pos, 1.5f, parent));
        builder.Build(blueprint, transform);

        // 設計図はワールド座標への変換器も兼ねる（ステージ中心が原点・y=0）
        blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn);
        Debug.Log($"開始地点: {blueprint.GridToWorld(spawn.X, spawn.Y)}");
    }
}
```

> 📖 **用語 — デリゲート（`delegate`）**: C# の「メソッドを値として持つ型」。`TileFactory` は `GameObject TileFactory(Vector3 position, float cellSize, Transform parent)`、`PlacementFactory` は `void PlacementFactory(in Placement placement, Vector3 worldPosition, Transform parent)` として宣言されています。`in` は「読み取り専用の参照渡し」で、構造体をコピーせず渡すための修飾子です（そのため引数を捨てるラムダも `(in Placement _, …)` と型を明記します）。

### 実戦例 — 迷宮のフルパイプラインと施工（デモの実物）

```csharp
// --- 1) 手作り部屋: 文字列＋凡例で書く。宝箱を内包した宝物庫と十字ホール ---
var legend = new RoomTemplateLegend()
    .Cell('#', CellType.Wall)
    .Cell('.', CellType.Floor)
    .Door('D')                                                   // 外へ必ず開口する出入口
    .Placement('C', CellType.Floor, PlacementKind.Chest, refId: 1); // 床＋宝箱の複合
var vault = RoomTemplate.Parse(new[] { "#####", "#C.C#", "#...D", "#####" }, legend);
var hall  = RoomTemplate.Parse(new[] { "##D##", "#...#", "D...D", "#...#", "##D##" }, legend);

// --- 2) 生成: パスを積む順番が意味を持つ（起点 → 距離帯 → 配置） ---
var blueprint = new GenerationPipeline()
    .Add(new FillPass(CellType.Wall))
    .Add(new MazeCarvePass(stage.BraidPermille))                 // マスターデータ由来の‰
    .Add(new TemplateRoomsPass(new[] { vault, hall }))           // 手作りを混ぜる
    .Add(new PlayerSpawnPass())                                  // ここで起点が決まる
    .Add(BiomeAssignPass.DistanceBands(                          // 入口=草原 / 最奥=溶岩
        new BiomeAssignPass.Band(500, GrassBiome),
        new BiomeAssignPass.Band(1000, LavaBiome)))
    .Add(new TileVariantPass(new TileVariantTable()              // 見た目のゆらぎも設計図へ焼く
        .Add(GrassBiome, CellType.Floor, 700, 300)               // 重み: バリアント0=700, 1=300
        .Add(LavaBiome, CellType.Floor, 300, 700)
        .Add(BiomeId.None, CellType.Wall, 800, 200)))
    .Add(new EnemyPlacementPass(1, 500, 1000,                    // 奥半分（500‰〜1000‰）に1体
        new PlacementTable().Add(_enemyId.Value, 1000)))
    .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
    .Add(new LandmarkPlacementPass(PlacementKind.Chest, 1, LandmarkRule.RandomWalkable))
    .Generate(stage.GenWidth, stage.GenHeight, cellSize: 1.5f, seed: (uint)(700 + stage.Id));

// --- 3) パレット: 素材登録。プレハブが届いたらこの行を Instantiate に差し替えるだけ ---
var palette = new StageAssetPalette()
    .Bind(GrassBiome, CellType.Floor, 0, PrimitiveTiles.FlatTile(new Color(0.4f, 0.62f, 0.34f)))
    .Bind(GrassBiome, CellType.Floor, 1, PrimitiveTiles.FlatTile(new Color(0.33f, 0.55f, 0.3f)))
    .Bind(LavaBiome,  CellType.Floor, 0, PrimitiveTiles.FlatTile(new Color(0.62f, 0.3f, 0.2f)))
    .Bind(BiomeId.None, CellType.Wall, 0, PrimitiveTiles.BlockTile(new Color(0.35f, 0.35f, 0.42f)));

// --- 4) 施工＋意味づけ: 「踏んだら何が起きるか」はアプリ側の仕事 ---
new StageBuilder(palette)
    .SetPlacement(PlacementKind.PlayerSpawn, (in Placement _, Vector3 __, Transform ___) => { })
    .SetPlacement(PlacementKind.EnemySpawn,  (in Placement _, Vector3 __, Transform ___) => { })
    .SetPlacement(PlacementKind.Exit,  MakeTrigger(new Color(0.2f, 0.9f, 0.4f)))
    .SetPlacement(PlacementKind.Shop,  MakeTrigger(new Color(0.95f, 0.85f, 0.2f)))
    .SetPlacement(PlacementKind.Chest, MakeTrigger(new Color(0.75f, 0.4f, 0.9f)))
    .Build(blueprint, _stageRoot.transform);                     // 親の下にまとまる

// --- 5) ユニットは設計図のスポーン点へ。壁があるので当たりを装着する ---
blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var playerSpawn);
BuildPlayerUnit(spec, blueprint.GridToWorld(playerSpawn.X, playerSpawn.Y) + Vector3.up,
    attachBody: true);   // attachBody = CapsuleCollider ＋ kinematic Rigidbody ＋ 自前モーター
```

市街側は下地と骨格のパスだけが違います（`FillPass(CellType.Floor)` → `BspDistrictPass(minDistrictSize: 7)` → `BuildingPlacementPass()` → `BiomeAssignPass.RegionBased(ResidentialBiome, MarketBiome)` → … → `LandmarkPlacementPass(PlacementKind.Shop, 0, LandmarkRule.RegionCenter, MarketBiome)`）。**施工側のコードは 1 行も変わりません**——設計図の形式が同じだからです。

### 実戦例 — 紐付け資産を実行時へ流し込む

エディタで作った資産は2行で施工へ渡ります。生成側（パイプライン）は一切変わりません。

```csharp
using Seed.StageGen;
using UnityEngine;

/// <summary>紐付け資産からステージを建てる（合成ルート側）。</summary>
public void BuildFromAsset(StagePaletteAsset palette, StageBlueprint blueprint, Transform parent)
{
    // 1) 地形: 役割→プレハブの対応表を組む（未登録の役割は内蔵プリミティブへ落ちる）
    var builder = new StageBuilder(palette.BuildPalette());

    // 2) 配置物: 出口・店・宝箱などの実体化を登録する
    palette.ApplyPlacements(builder);

    // 3) 施工不要な種別も空実装で登録しておく（未登録は警告になる規約）
    builder.SetPlacement(PlacementKind.PlayerSpawn,
        (in Placement _, Vector3 __, Transform ___) => { });

    builder.Build(blueprint, parent);
}
```

Play 前に不備を確かめたいときは、実行時にも同じ検証を使えます。

```csharp
var problems = palette.Validate();      // 空リストなら合格
for (var i = 0; i < problems.Count; i++)
{
    Debug.LogWarning($"[StagePalette] {problems[i]}");
}
```

この資産が持つのは「役割 → プレハブ」の対応だけで、**抽選は一切しません**
（バリアントの抽選は生成側で設計図へ焼き込み済み）。施工で乱数を引くと
「同じシードなのに見た目が違う」が起きてリプレイと矛盾するためです。

## 5. 仕組み

### なぜ施工で抽選しないのか

この基盤の中心的な決断は「抽選はすべて生成側（純C#）で終わらせ、施工側は設計図を読んで建てるだけにする」ことです。`StageGrid` がセル種別だけでなく**素材バリアント番号とバイオームIDまで**3 レイヤーで持つのは、そのためです。「床の色を 700:300 の重みで振り分ける」という見た目の抽選まで `TileVariantPass` が設計図に焼き込み、`StageBuilder` は焼かれた番号を引くだけになります。

施工側で乱数を引くとどうなるか、具体的に 3 つ壊れます。

1. **リプレイと映像が一致しない**。入力列を再生して試合を再現しても、床や壁の見た目が毎回変わります。「同じシード・同じ入力なら同じ画面」が言えなくなり、不具合の再現手順が成立しません。
2. **EditMode テストで検証できない**。抽選が `GameObject` の生成と混ざると、迷路の全域連結・敵の距離帯・バリアントの重みといった性質を確かめるのに Unity のシーンが必要になります。生成が純C#で完結しているから、`StageGenTests` は同一シードの設計図をセル単位で比較できます。
3. **将来のロックステップ通信と矛盾する**。地形はロジックへの入力です。抽選の回数や順序が実行環境で揺れる余地を残すと、端末間で世界が食い違います。

裏返しの制約として、パスの中で使える乱数は**引数で渡された `DeterministicRandom` だけ**です。`UnityEngine.Random` を 1 行混ぜた瞬間に決定性は失われます。

### パスごとの乱数 Fork

> 📖 **用語 — Fork（乱数ストリームの分岐）**: 親の乱数を 1 回消費し、その値を splitmix32 風に撹拌して新しいシードにした**子ストリーム**を作る操作。親子の相関が切れるので、子の消費回数が変わっても他の子には影響しません。

`GenerationPipeline.Generate` は `new DeterministicRandom(seed)` を親として作り、パス i には `parent.Fork()` の結果を渡します。パスが自分の中で乱数を何回引いても、他のパスの乱数列には一切影響しません。この設計のおかげで、**パイプラインの末尾にパスを足しても既存ステージの地形が変わりません**（`StageGenTests.Pipeline_AppendingPass_DoesNotDisturbEarlierPasses` がこれを検証しています）。逆に**途中へパスを挿入すると以降の Fork 順がずれる**ので、既存ステージのシード互換は壊れます。

### 迷路: 穴掘り法と braid

> 📖 **用語 — 穴掘り法（再帰的バックトラッカー）**: 全面が壁の状態から、「ランダムな方向へ 2 セル飛び、間の壁ごと掘って前進」「掘れる方向が無ければスタックで後退」を繰り返す迷路生成法。掘れた床は必ず既掘り部分と繋がるので、結果は**全域連結で閉路のない完全迷路**になります。

`MazeCarvePass.Carve` は座標 (1,1) から開始し、4 方向を Fisher-Yates シャッフルで並べ替えてから試します（この並べ替えも渡された乱数だけを使います）。外周 1 セルは掘らないため、必ず壁で囲まれます。1 セルおきに床を掘る構造上、**グリッドは奇数サイズが推奨**です（偶数だと外周側に太い壁が残ります）。

> 📖 **用語 — braid（編み込み）**: 完全迷路の行き止まりを一部貫通させて閉路を作る後処理。行き止まり（歩行可能な隣が 1 つ以下のセル）を走査し、`braidPermille`‰ の確率で「壁の向こうが床」の壁を 1 枚抜きます。完全迷路のままだと戦闘が一本道の追い掛けっこになるので、逃げ道を作るための調整です。デモの迷宮は 200‰ です。

### 街: BSP と建物

> 📖 **用語 — BSP（二分空間分割）**: 領域を再帰的に 2 つへ割っていく分割法。`BspDistrictPass` は分割方向を長辺優先（同じ長さなら乱数）で選び、分割位置は「両側が `minDistrictSize` を下回らない範囲」で乱択します。

要点は**分割線そのものを道路（`CellType.Road`）として引く**ことです。分割線は親領域の道路まで届かせるので、道路網は構造上必ず全域が繋がります。外周 1 セルも道路の環にしてあります。これ以上割れなくなった葉が「区画」として `Blueprint.Regions` に記録され、下流の `BuildingPlacementPass`（建物）・`BiomeAssignPass.RegionBased`（区画ごとの塗り）・`LandmarkPlacementPass`（`RegionCenter` 規則）が同じ矩形リストを読みます。建物は区画の内側に歩道 1 セルを残して置かれ、4 辺のどこか 1 つに `CellType.Door` を開けるため、建てても歩行可能領域の連結は壊れません。

### 距離帯バイオームと配置

> 📖 **用語 — BFS（幅優先探索）**: 起点から近い順にグリッドを辿る探索法。`StageGrid.ComputeDistances(x, y)` は歩行可能セルの歩数マップ（到達不能・非歩行は −1）を返します。走査順と近傍の並びが固定なので結果は決定的で、距離帯・最遠点・連結性検査の共通道具になっています。

`BiomeAssignPass.DistanceBands` は起点（`PlayerSpawn` があればそこ、無ければ走査順で最初の歩行可能セル）から BFS 距離を求め、`距離 × 1000 ÷ 最大距離` の‰を帯に照らして塗ります。壁など距離 −1 のセルは 2 周目で**隣接する塗り済みセルの帯を継承**し（孤立した内部は先頭の帯へ倒す）、壁の見た目も帯に馴染みます。`EnemyPlacementPass` と `LandmarkPlacementPass` も同じ距離マップを使い、前者は帯に入る空きセルを走査順に集めてから抽選、後者は `Farthest`（最遠点）/ `RandomWalkable`（到達可能セルの乱択）/ `RegionCenter`（区画中心から矩形リングを広げて最初の歩行可能セル）で 1 点だけ置きます。いずれも `blueprint.IsOccupied(x, y)` で既配置セルを除外するので、配置物同士は重なりません。

### 手作り部屋の混入と連結性の回復

`TemplateRoomsPass` は各テンプレートを乱択位置へスタンプします（既存の `Regions` と余白 1 で重ならない位置を最大 `attemptsPerTemplate` 回試し、見つからなければ静かに諦める＝生成自体は失敗しません）。`Door` セルからは外向きに直進で床を掘り、既存の歩行可能セルへ当たるまで進むので、部屋は必ず開口します。

> 📖 **用語 — 連結成分ラベリング**: グリッドの歩行可能セルを、繋がっている塊ごとに番号を振る処理。`ReconnectIslands` は成分数が 2 以上なら「異なる成分を隔てる厚さ 1〜3 の壁」を 1 本掘って橋を架け、成分が 1 つになるまで最大 64 回繰り返します。スタンプで迷路の通路が上書きされ島ができても、全域連結が必ず回復します。

### 施工のフォールバック連鎖

> 📖 **用語 — フォールバック連鎖**（本プロジェクト固有）: 具体的な登録から順に探し、無ければ徐々に一般的な登録へ落ちていく検索順。`StageAssetPalette.Resolve` は **(バイオーム, セル, バリアント) → (None, セル, バリアント) → (None, セル, 0) → 内蔵プリミティブ** の順に探し、**必ず何かを返します**。内蔵プリミティブは歩行可能セルなら薄板（`FlatTile`）、それ以外なら箱（`BlockTile`。`Building` は高さ 3、他は 2）で、色の登録も無い未知セル種別はマゼンタになります。

配置物側はフォールバックしません。`SetPlacement` に無い種別は黙殺せず `Debug.LogWarning` を出します——「種別を増やしたが実体化を書き忘れた」事故を検知するための規約で、施工不要な `PlayerSpawn` / `EnemySpawn` でも空実装の登録が必要です。

### 三大規約との関係

- **状態は Tick、艶は Update**: 生成と施工はフェーズ入場時の 1 回だけで、毎フレームの処理ではありません。そのうえで、設計図は「艶」だけの持ち物ではない点が重要です。`Placements` の座標は `GridToWorld` を通ってユニットの初期 `Pose`（Tick 側の真実）になり、壁・建物のコライダーは移動モーター（`Sample_KinematicMotor` の collide-and-slide）経由で Tick 側の移動解決に効きます。トリガーの踏み判定も `TickPhase.Drain` に登録された純C#の距離判定（0.8m・XZ 平面）で、コライダーには依存しません。純粋に「艶」なのは `StageAssetPalette` が決める見た目（プレハブ・色）だけで、そこを差し替えてもロジックは 1 ビットも動きません。
- **方針は App**: どのパスをどの順で積むか、帯の境界を何‰にするか、どの色・プレハブを `Bind` するか、緑を踏んだら何が起きるかは、すべて App（`Sample_BattlePhase.BuildMazePipeline` / `BuildTownPipeline` / `BuildPalette` / `MakeTrigger`）が決めています。基盤側のパスとビルダーは実行部に徹します。
- **命令の処理者は 1 基盤**: 出口を踏んだとき StageGen 側はフェーズを変えません。App が `ChangePhaseCommand` を Hub へ発行し、処理者は Flow 基盤ただ 1 つです。生成基盤が画面遷移や戦闘状態に直接触らないので、「なぜホームに戻ったか」の答えは常に Flow の命令ログに残ります。

## 6. よくあるつまずき

- **症状**: 同じステージなのに毎回地形が違う → **原因**: パス内で `UnityEngine.Random` / `DateTime.Now` / `GetHashCode()` を使った → **対処**: `Execute` に渡された `DeterministicRandom` だけを乱数源にする
- **症状**: パスを 1 つ差し込んだら既存ステージの迷路が別物になった → **原因**: パイプラインの**途中**に挿入したため以降の `Fork()` 順がずれた → **対処**: 既存ステージの互換を保ちたいときは末尾に足す（末尾追加は先行パスの出力を変えないことがテストで保証されている）
- **症状**: Console に `[StageBuilder] 実体化表に無い配置種別` の警告 → **原因**: `SetPlacement` の登録漏れ → **対処**: 施工不要でも `(in Placement _, Vector3 __, Transform ___) => { }` の空実装を登録する
- **症状**: 一部のタイルがマゼンタで建つ → **原因**: パレットにも内蔵色にも無いセル種別（自作の 100 番台など） → **対処**: `StageAssetPalette.Bind` で素材を登録するか、`SetFallbackColor(cell, color)` で内蔵色を足す
- **症状**: 迷路の外周側に太い壁が残る → **原因**: グリッドが偶数サイズ → **対処**: `genWidth` / `genHeight` を奇数にする（21・31 など）
- **症状**: 迷路が一本道で戦闘が追い掛けっこになる → **原因**: `braidPermille` が 0（完全迷路） → **対処**: 200‰ 前後を渡して行き止まりを貫通させる
- **症状**: `ArgumentOutOfRangeException: グリッドは3x3以上` → **原因**: `Generate` の width / height が 3 未満 → **対処**: 3 以上にする
- **症状**: `ArgumentException: 凡例に無い文字 'X'` → **原因**: `RoomTemplateLegend` への登録漏れ → **対処**: `.Cell()` / `.Door()` / `.Placement()` でその文字を登録する（`ArgumentException: 行の長さが不揃い` は全行を同じ文字数に揃える）
- **症状**: 手作り部屋がステージに現れない → **原因**: 既存区画と余白 1 で重なる位置ばかり引いた、またはテンプレートがグリッドに入らない（設計上、置けなければ静かに諦める） → **対処**: `attemptsPerTemplate` を増やす、グリッドを広げる、テンプレートを小さくする
- **症状**: バイオームが塗られない・帯が意図とずれる → **原因**: `BiomeAssignPass` を `PlayerSpawnPass` より前に積んだため、起点が「走査順で最初の歩行可能セル」に落ちた → **対処**: 起点に依存するパス（距離帯バイオーム・敵配置・最遠点）は `PlayerSpawnPass` の**後**に積む
- **症状**: 敵が 1 体も置かれない → **原因**: 指定した距離帯（下限‰〜上限‰）に入る空きセルが無い → **対処**: 帯を広げる、または `count` を減らす
- **症状**: プレイヤーが壁を通り抜ける → **原因**: ユニットに当たりを装着していない（`attachBody: false`） → **対処**: 生成ステージではカプセルコライダー＋kinematic Rigidbody の自前モーターを装着する（`Sample_BattlePhase.AttachBody` 参照。平地ステージのプレイヤーには `BuildPlayerUnit` が常時装着します）
- **症状**: 敵を複数配置したのに 1 体しか出ない → **原因**: 仕様。GameCore のサンプル世界が Hunter / Monster の 1v1 固定のため、実体化するのは最初の `EnemySpawn` だけ（配置基盤側は複数・テーブル対応済み） → **対処**: ロジックを N 体対応にしたうえで、`BuildGeneratedStage` の実体化をループにする

## 7. 増やす・拡張する

- **地形の種類を増やす（洞窟・塔・ダンジョン…）**: `Assets/Script/StageGen/Runtime/Passes/` に `IGenerationPass` 実装を 1 つ書き（`Name` と `Execute`）、パイプラインへ `.Add()` する。既製パスが実装の手本（`FillPass` は 25 行程度）
- **ステージを増やす**: `Sample_MasterCatalog.Build` に `Sample_StageSpec` を 1 件（`generatorKind`: 0=平地 / 1=迷路 / 2=街、`genWidth` / `genHeight` は奇数推奨、`braidPermille`）＋ `Sample_HomePhase.Tick` にメニュー行と出撃キーを足す。ステージ ID は 201 番から（`catalog.ValidateGlobalIdUniqueness()` が全体の一意性を検証）
- **美術アセットへ差し替える（エディタ経路・推奨）**: メニュー `Seed/Stage Palette` で紐付け資産を作り、プレハブを割り当てて `palette.BuildPalette()` / `palette.ApplyPlacements(builder)` を渡す。**コードは触らない**
- **美術アセットへ差し替える（コード経路）**: `Sample_BattlePhase.BuildPalette` の `.Bind(バイオーム, セル種別, バリアント, PrimitiveTiles.〜)` を、プレハブを `Instantiate` する `TileFactory` に置き換える。生成ロジックは一切変わらない
- **配置物の種類を増やす**: `new PlacementKind(100)` 以降で発番 → `LandmarkPlacementPass` を 1 行 `.Add()`（規則は `Farthest` / `RandomWalkable` / `RegionCenter`）→ `StageBuilder.SetPlacement` で実体化を 1 行登録
- **セル種別を増やす**: `new CellType(100)` 以降で発番。**奇数 = 歩行可能**の規約（例: 101=浅瀬（可）/ 102=深い水路（不可））。見た目は `Bind` か `SetFallbackColor`
- **手作り部屋を増やす**: `RoomTemplateLegend`（`.Cell(char, CellType)` / `.Door(char)` / `.Placement(char, 下地セル, PlacementKind, refId)`）＋ `RoomTemplate.Parse(string[], legend)` → `TemplateRoomsPass` へ渡す。行文字列を `EntityDefinitionAsset` 派生の SO に持たせれば、企画が Inspector で部屋を書けるようになる
- **バイオームを塗る**: `BiomeAssignPass.DistanceBands(new Band(上限‰, BiomeId), …)`（距離帯）または `BiomeAssignPass.RegionBased(候補BiomeId…)`（区画抽選）
- **床・壁の色ゆらぎ**: `TileVariantTable.Add(BiomeId, CellType, 重み…)` → `TileVariantPass`。表に行が無い組み合わせは乱数を消費せずバリアント 0 に落ちる（乱数消費数を一定に保つため）
- **敵の出現テーブル**: `new PlacementTable().Add(refId, weight)` を `EnemyPlacementPass` の第 4 引数へ。第 5 引数に `Dictionary<int, PlacementTable>`（キー = バイオームID）を渡せば「溶岩地帯だけ強敵」がデータで組める。重み 0 の行は絶対に選ばれない

## 8. 関連ファイルとテスト

- `Assets/Script/StageGen/Runtime/GenerationPipeline.cs` — パス列の実行と乱数の Fork
- `Assets/Script/StageGen/Runtime/StageBlueprint.cs` / `StageGrid.cs` / `GridRect.cs` — 設計図・3 レイヤーグリッド・BFS 距離マップ
- `Assets/Script/StageGen/Runtime/IGenerationPass.cs` — 生成パスの契約
- `Assets/Script/StageGen/Runtime/CellType.cs` / `BiomeId.cs` / `PlacementKind.cs` — 増える語彙の値型と `Placement`
- `Assets/Script/StageGen/Runtime/RoomTemplate.cs` — 手作り部屋の文字列パーサと凡例
- `Assets/Script/StageGen/Runtime/TileVariantTable.cs` / `PlacementTable.cs` — バリアント重み表・出現テーブル
- `Assets/Script/StageGen/Runtime/Builder/StagePaletteAsset.cs` — プレハブ紐付け資産と表示名（`StageGenNames`）
- `Assets/Script/StageGen/Editor/StagePaletteWindow.cs` — セットアップウィンドウ（`Seed/Stage Palette`）
- `Assets/Script/StageGen/Runtime/Passes/` — `FillPass` / `MazeCarvePass` / `TemplateRoomsPass` / `BspDistrictPass` / `BuildingPlacementPass` / `BiomeAssignPass` / `TileVariantPass` / `PlayerSpawnPass` / `EnemyPlacementPass` / `LandmarkPlacementPass`
- `Assets/Script/StageGen/Runtime/Builder/StageAssetPalette.cs` — 素材登録表・フォールバック連鎖・`PrimitiveTiles`
- `Assets/Script/StageGen/Runtime/Builder/StageBuilder.cs` — 施工者と未登録警告
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` — デモ統合（`BuildGeneratedStage` / `BuildMazePipeline` / `BuildTownPipeline` / `BuildPalette` / `MakeTrigger` / `CheckTriggers` / `AttachBody`）
- `Assets/Script/App/Samples/Sample_MasterCatalog.cs` / `Sample_StageSpec.cs` — ステージ定義（迷宮 203 / 市街 204）
- テスト: `Assets/Script/StageGen/Tests/Editor/StageGenTests.cs` — 決定性（3 レイヤー＋配置の完全一致）・全域連結・braid の効果・道路と出入口・距離帯・重み抽選・テンプレ連結性・Fork の独立性・`GridToWorld`。生成が純C#だから EditMode で全部検証できます

[← 前: 10_AI](10_AI.md) | [索引](00_Roadmap.md) | [次: 12_GameCore →](12_GameCore.md)
