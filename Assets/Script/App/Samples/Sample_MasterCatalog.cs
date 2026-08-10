using Seed.Data;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】このアプリのマスターデータカタログ（ユニット仕様＋ステージ仕様）。
    /// 実プロジェクトでは ScriptableObject / JSON → MasterDataLoader で流し込む部分。
    /// 「キャラやステージを増やす＝ここに1件足す（将来はデータを1つ足す）」。
    /// ID帯の運用: 1〜99=ユニット / 201〜=ステージ（型が違ってもIDは全体で一意にする）。
    /// </summary>
    public static class Sample_MasterCatalog
    {
        /// <summary>ステージ1（草原）。</summary>
        public static readonly StageId Stage1 = new StageId(201);

        /// <summary>ステージ2（火山）。</summary>
        public static readonly StageId Stage2 = new StageId(202);

        /// <summary>ステージ3（自動生成: 迷宮）。</summary>
        public static readonly StageId Stage3 = new StageId(203);

        /// <summary>ステージ4（自動生成: 市街）。</summary>
        public static readonly StageId Stage4 = new StageId(204);

        /// <summary>ステージ5（揺れものデモ: UnityChan・Playables 直駆動）。</summary>
        public static readonly StageId Stage5 = new StageId(205);

        /// <summary>カタログを組み立てる（起動時に1回）。</summary>
        public static MasterDataSet Build(CharacterId playerId, CharacterId enemyId)
        {
            var catalog = new MasterDataSet();

            // ユニット仕様（定義ID = CharacterId と同値運用）
            catalog.Add(new Sample_UnitSpec(playerId.Value, "Hunter",
                moveSpeed: 4f, attackSeconds: 0.4f, staggerSeconds: 0.25f));
            catalog.Add(new Sample_UnitSpec(enemyId.Value, "Monster",
                moveSpeed: 2f, attackSeconds: 0.5f, staggerSeconds: 0.25f));

            // ステージ仕様（定義ID = StageId と同値運用）
            catalog.Add(new Sample_StageSpec(Stage1.Value, "Grassland", "草原（敵の攻撃: ゆっくり）",
                groundColor: new Color(0.35f, 0.6f, 0.3f), groundScale: 2f, enemyAttackInterval: 4f));
            catalog.Add(new Sample_StageSpec(Stage2.Value, "Volcano", "火山（敵の攻撃: 速い）",
                groundColor: new Color(0.55f, 0.25f, 0.15f), groundScale: 1.5f, enemyAttackInterval: 2.5f));

            // 自動生成ステージ（地形・配置は Seed.StageGen がシードから決定的に作る）
            catalog.Add(new Sample_StageSpec(Stage3.Value, "Labyrinth", "迷宮（自動生成・出口を探せ）",
                groundColor: Color.gray, groundScale: 1f, enemyAttackInterval: 3.5f,
                generatorKind: 1, genWidth: 21, genHeight: 21, braidPermille: 200));
            catalog.Add(new Sample_StageSpec(Stage4.Value, "Township", "市街（自動生成・店に寄れる）",
                groundColor: Color.gray, groundScale: 1f, enemyAttackInterval: 3.0f,
                generatorKind: 2, genWidth: 31, genHeight: 31));

            // 揺れものデモ（UnityChan・Playables 直駆動。標準は StarterAssets・Controller 駆動）
            catalog.Add(new Sample_StageSpec(Stage5.Value, "SpringDemo", "揺れものデモ（UnityChan）",
                groundColor: new Color(0.35f, 0.55f, 0.4f), groundScale: 2f, enemyAttackInterval: 4f,
                playerModelKind: 1));

            catalog.ValidateGlobalIdUniqueness();
            return catalog;
        }
    }
}
