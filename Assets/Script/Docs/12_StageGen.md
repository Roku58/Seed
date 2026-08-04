# 12. Seed.StageGen — ステージ自動生成（迷路・街・配置）

パスの列で**設計図**（`StageBlueprint`）を決定的に生成し、**施工**（`StageBuilder`＋
`StageAssetPalette`）で GameObject を建てる。施工は一切抽選しない＝同じシードなら
バイト単位で同一の設計図・同じ見た目。パスごとに乱数を Fork するため、
パスを追加してもシード互換が壊れにくい。

## デモで確認する（ホームから [4] / [5]）

- **[4] 迷宮**（21×21・シード903）: 全域連結の迷路。入口側=草原（緑床）→ 最奥=溶岩（赤茶床）の
  バイオーム距離帯。文字列テンプレートの宝物庫・ホールがスタンプされる。
  緑=出口（BFS 最遠点）・紫=宝箱・敵は奥の距離帯に配置
- **[5] 市街**（31×31・シード904）: BSP 区画＋建物。住宅街（茶）と市場（青）の区画バイオーム。
  黄=店（市場区画の中心）→ 踏むと**ショップフェーズへ**
- 同じステージへ再入すると同じ地形・同じ配置（決定性）
- Console: SetPlacement 未登録の配置種別は警告が出る／未知セル種別はマゼンタで建つ（見落とし検知）

## 最小コード（空 GameObject に付けて Play）

```csharp
using Seed.StageGen;
using UnityEngine;

public sealed class MiniStageDemo : MonoBehaviour
{
    private void Start()
    {
        // 生成: パス列 → 設計図（同じ seed なら毎回同じ迷路）
        var blueprint = new GenerationPipeline()
            .Add(new FillPass(CellType.Wall))
            .Add(new MazeCarvePass(braidPermille: 200))
            .Add(new PlayerSpawnPass())
            .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
            .Generate(width: 21, height: 21, cellSize: 1.5f, seed: 903);

        // 施工: パレット未登録でも内蔵プリミティブで必ず建つ
        var builder = new StageBuilder()
            .SetPlacement(PlacementKind.PlayerSpawn, (in Placement _, Vector3 __, Transform ___) => { })
            .SetPlacement(PlacementKind.Exit, (in Placement _, Vector3 pos, Transform parent) =>
                PrimitiveTiles.MarkerTile(new Color(0.2f, 0.9f, 0.4f))(pos, 1.5f, parent));
        builder.Build(blueprint, transform);
    }
}
```

## 増やすとき（すべて登録1行の流儀）

| やりたいこと | 書く場所 |
|---|---|
| 地形の種類（洞窟・塔…） | `IGenerationPass` 実装1つ → `.Add()` |
| ステージ追加 | `Sample_MasterCatalog` に Spec 1件（generatorKind/genWidth/genHeight/braidPermille）＋ホームにメニュー行 |
| 美術アセット差し替え | `StageAssetPalette.Bind(バイオーム, セル, バリアント, 工場)` の行をプレハブ Instantiate に差し替え（生成側は不変） |
| 意味オブジェクト追加 | PlacementKind 発番（100〜）→ `LandmarkPlacementPass` 1行（Farthest / RandomWalkable / RegionCenter）→ `SetPlacement` 1行 |
| 手作り部屋 | `RoomTemplate.Parse(文字列, 凡例)` → `TemplateRoomsPass` へ（宝箱内包も凡例で書ける） |
| バイオーム | `BiomeAssignPass.DistanceBands(...)`（距離帯）/ `RegionBased(...)`（区画抽選） |
| 床・壁の色ゆらぎ | `TileVariantTable.Add(バイオーム, セル, 重み‰...)` → `TileVariantPass` |
| 敵の出現テーブル | `EnemyPlacementPass(数, 距離帯下限‰, 距離帯上限‰, PlacementTable, バイオーム別テーブル)`（デモ実例: `new EnemyPlacementPass(1, 500, 1000, table)`） |

パレットはフォールバック連鎖（バイオーム→既定→内蔵プリミティブ）——**アセット0個でも必ず建つ**。

## ハマりどころ

- パス内の乱数は渡された DeterministicRandom のみ（UnityEngine.Random を混ぜると決定性が壊れる）
- 迷路はグリッド奇数サイズ推奨。完全迷路は一本道になりがち→ braidPermille で逃げ道を作る
- 施工不要の配置種別（PlayerSpawn 等）も**空実装の SetPlacement 登録が必要**（書き忘れ検知の規約）
- 生成ステージでは壁があるため CharacterController＋CharacterControllerMotionSolver の装着が必要
  （`Sample_BattlePhase.AttachBody` 参照。平地では付けない）
- 敵の実体化は最初の EnemySpawn 1体のみ（GameCore が 1v1 固定のため。配置側は複数対応済み）

主要ファイル: `Assets/Script/StageGen/Runtime/`（Passes/ と Builder/）。
デモ統合: `Sample_BattlePhase.cs` の BuildGeneratedStage / BuildMazePipeline / BuildTownPipeline / BuildPalette。
テスト: `Assets/Script/StageGen/Tests/Editor/StageGenTests.cs`
