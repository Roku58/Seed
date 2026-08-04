using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 敵を戦略配置する（出現テーブル×BFS距離帯）。
    ///
    /// プレイヤー起点からのBFS距離を‰に正規化し、指定の距離帯に入る歩行可能セルから
    /// 抽選で配置する——「入口近くは弱く・奥は強く」を帯の設定だけで表現できる。
    /// 敵の種類は出現テーブル（refId＋重み）から抽選し、バイオーム別テーブルが
    /// あればそちらを優先する（溶岩地帯だけ強敵、のようなテーマ差をデータで組める）。
    /// 配置済みセルは候補から除外（重なり防止）。すべて決定的。
    /// </summary>
    public sealed class EnemyPlacementPass : IGenerationPass
    {
        /// <summary>配置数。</summary>
        private readonly int _count;

        /// <summary>距離帯の下限（‰）。</summary>
        private readonly int _bandMinPermille;

        /// <summary>距離帯の上限（‰）。</summary>
        private readonly int _bandMaxPermille;

        /// <summary>既定の出現テーブル。</summary>
        private readonly PlacementTable _defaultTable;

        /// <summary>バイオーム別の出現テーブル（無いバイオームは既定を使う）。</summary>
        private readonly Dictionary<int, PlacementTable> _biomeTables;

        /// <summary>EnemyPlacementPass を生成する。</summary>
        public EnemyPlacementPass(int count, int bandMinPermille, int bandMaxPermille,
            PlacementTable defaultTable, Dictionary<int, PlacementTable> biomeTables = null)
        {
            _count = count;
            _bandMinPermille = bandMinPermille;
            _bandMaxPermille = bandMaxPermille;
            _defaultTable = defaultTable ?? new PlacementTable();
            _biomeTables = biomeTables;
        }

        /// <summary>パス名。</summary>
        public string Name => "EnemyPlacement";

        /// <summary>距離帯の候補から抽選して敵を置く。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;

            // 起点（PlayerSpawnPass の後に流す規約。無ければ最初の歩行可能セル）
            int startX;
            int startY;
            if (blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn))
            {
                startX = spawn.X;
                startY = spawn.Y;
            }
            else if (!grid.TryFindFirstWalkable(out startX, out startY))
            {
                return;
            }

            var distances = grid.ComputeDistances(startX, startY);
            var maxDistance = 1;
            for (var i = 0; i < distances.Length; i++)
            {
                if (distances[i] > maxDistance)
                {
                    maxDistance = distances[i];
                }
            }

            // 帯に入る候補を走査順（決定的）で集める
            var candidates = new List<(int X, int Y)>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var distance = distances[y * grid.Width + x];
                    if (distance <= 0 || blueprint.IsOccupied(x, y))
                    {
                        continue; // 到達不能・起点そのもの・既配置は除外
                    }
                    var permille = distance * 1000 / maxDistance;
                    if (permille >= _bandMinPermille && permille <= _bandMaxPermille)
                    {
                        candidates.Add((x, y));
                    }
                }
            }

            for (var i = 0; i < _count && candidates.Count > 0; i++)
            {
                var index = random.NextInt(candidates.Count);
                var (x, y) = candidates[index];
                candidates.RemoveAt(index); // 重なり防止

                var table = ResolveTable(grid.GetBiome(x, y));
                blueprint.AddPlacement(PlacementKind.EnemySpawn, table.Pick(random), x, y);
            }
        }

        /// <summary>バイオーム別テーブルを引く（無ければ既定）。</summary>
        private PlacementTable ResolveTable(BiomeId biome)
        {
            if (_biomeTables != null && _biomeTables.TryGetValue(biome.Value, out var table))
            {
                return table;
            }
            return _defaultTable;
        }
    }
}
