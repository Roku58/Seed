using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// バイオーム（領域テーマ）を塗る。
    ///
    /// 2つの塗り方を提供する:
    /// - DistanceBands … 起点からのBFS距離の割合（‰）でバイオーム帯を塗る
    ///   （「入口は草原、最奥は溶岩」。壁は隣接する歩行可能セルの帯を継承する）
    /// - RegionBased  … 区画（Regions）ごとに候補から抽選して塗る（「住宅街と市場」）
    ///
    /// 塗られたバイオームは下流の分岐キーになる:
    /// 素材バリアント（TileVariantPass）・敵の出現テーブル（EnemyPlacementPass）・
    /// 施工パレット（StageAssetPalette）——「奥ほど強い敵と違う見た目」がデータで組める。
    /// </summary>
    public sealed class BiomeAssignPass : IGenerationPass
    {
        /// <summary>距離帯1件（‰の上限とバイオーム）。</summary>
        public readonly struct Band
        {
            /// <summary>この帯の上限（起点からの距離の‰。1000=最遠）。</summary>
            public readonly int UpToPermille;

            /// <summary>塗るバイオーム。</summary>
            public readonly BiomeId Biome;

            /// <summary>Band を生成する。</summary>
            public Band(int upToPermille, BiomeId biome)
            {
                UpToPermille = upToPermille;
                Biome = biome;
            }
        }

        /// <summary>距離帯（DistanceBands モード。近い順に並べる）。</summary>
        private readonly Band[] _bands;

        /// <summary>区画抽選の候補（RegionBased モード）。</summary>
        private readonly BiomeId[] _regionCandidates;

        /// <summary>距離帯モードで生成する（帯は近い順）。</summary>
        public static BiomeAssignPass DistanceBands(params Band[] bands)
        {
            return new BiomeAssignPass(bands, null);
        }

        /// <summary>区画モードで生成する（区画ごとに候補から等確率抽選。区画外は最初の候補）。</summary>
        public static BiomeAssignPass RegionBased(params BiomeId[] candidates)
        {
            return new BiomeAssignPass(null, candidates);
        }

        /// <summary>BiomeAssignPass を生成する（ファクトリメソッド経由で使う）。</summary>
        private BiomeAssignPass(Band[] bands, BiomeId[] regionCandidates)
        {
            _bands = bands;
            _regionCandidates = regionCandidates;
        }

        /// <summary>パス名。</summary>
        public string Name => "BiomeAssign";

        /// <summary>バイオームを塗る。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            if (_bands != null)
            {
                AssignByDistance(blueprint);
            }
            else if (_regionCandidates != null && _regionCandidates.Length > 0)
            {
                AssignByRegion(blueprint, random);
            }
        }

        /// <summary>距離帯で塗る。</summary>
        private void AssignByDistance(StageBlueprint blueprint)
        {
            var grid = blueprint.Grid;

            // 起点: PlayerSpawn 配置があればそこ、無ければ最初の歩行可能セル
            int startX;
            int startY;
            if (blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn))
            {
                startX = spawn.X;
                startY = spawn.Y;
            }
            else if (!grid.TryFindFirstWalkable(out startX, out startY))
            {
                return; // 歩ける場所が無い＝塗る意味がない
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

            // 1回目: 歩行可能セルを帯で塗る
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var distance = distances[y * grid.Width + x];
                    if (distance < 0)
                    {
                        continue;
                    }
                    grid.SetBiome(x, y, BandOf(distance * 1000 / maxDistance));
                }
            }

            // 2回目: 壁など未塗りのセルは、隣接する塗り済みセルの帯を継承する
            //（壁の見た目も帯に馴染ませるため。孤立した内部は最初の帯に倒す）
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = y * grid.Width + x;
                    if (distances[index] >= 0)
                    {
                        continue;
                    }
                    grid.SetBiome(x, y, NeighborBiome(grid, distances, x, y));
                }
            }
        }

        /// <summary>距離‰から帯を引く。</summary>
        private BiomeId BandOf(int permille)
        {
            for (var i = 0; i < _bands.Length; i++)
            {
                if (permille <= _bands[i].UpToPermille)
                {
                    return _bands[i].Biome;
                }
            }
            return _bands[_bands.Length - 1].Biome;
        }

        /// <summary>隣接する塗り済みセルのバイオーム（無ければ最初の帯）。</summary>
        private BiomeId NeighborBiome(StageGrid grid, int[] distances, int x, int y)
        {
            if (grid.InBounds(x + 1, y) && distances[y * grid.Width + x + 1] >= 0) { return grid.GetBiome(x + 1, y); }
            if (grid.InBounds(x - 1, y) && distances[y * grid.Width + x - 1] >= 0) { return grid.GetBiome(x - 1, y); }
            if (grid.InBounds(x, y + 1) && distances[(y + 1) * grid.Width + x] >= 0) { return grid.GetBiome(x, y + 1); }
            if (grid.InBounds(x, y - 1) && distances[(y - 1) * grid.Width + x] >= 0) { return grid.GetBiome(x, y - 1); }
            return _bands[0].Biome;
        }

        /// <summary>区画ごとに抽選して塗る（区画外＝道路等は最初の候補）。</summary>
        private void AssignByRegion(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    grid.SetBiome(x, y, _regionCandidates[0]);
                }
            }

            var regionBiomes = new List<BiomeId>(blueprint.Regions.Count);
            for (var i = 0; i < blueprint.Regions.Count; i++)
            {
                regionBiomes.Add(_regionCandidates[random.NextInt(_regionCandidates.Length)]);
            }
            for (var i = 0; i < blueprint.Regions.Count; i++)
            {
                var region = blueprint.Regions[i];
                for (var y = region.Y; y < region.YMax; y++)
                {
                    for (var x = region.X; x < region.XMax; x++)
                    {
                        grid.SetBiome(x, y, regionBiomes[i]);
                    }
                }
            }
        }
    }
}
