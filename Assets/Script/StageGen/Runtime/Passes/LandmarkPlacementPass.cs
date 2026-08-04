using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>配置規則（意味オブジェクトをどこに置くか）。</summary>
    public enum LandmarkRule
    {
        /// <summary>起点からのBFS最遠点（出口向け）。</summary>
        Farthest = 0,

        /// <summary>歩行可能セルから乱択（宝箱・イベント向け）。</summary>
        RandomWalkable = 1,

        /// <summary>区画の中心近傍（街のショップ向け。バイオームで区画を絞れる）。</summary>
        RegionCenter = 2,
    }

    /// <summary>
    /// 意味のあるオブジェクト（出口・ショップ・宝箱・イベント…）を1つ置く。
    ///
    /// 「何を・どの規則で置くか」だけを引数にした汎用パス——
    /// 新しい種類の配置は PlacementKind を発番してこのパスを1行足すだけ。
    /// 踏んだときに何が起きるかは消費側（フェーズの配置表）の仕事で、生成は関知しない。
    /// </summary>
    public sealed class LandmarkPlacementPass : IGenerationPass
    {
        /// <summary>置く種別。</summary>
        private readonly PlacementKind _kind;

        /// <summary>参照ID（意味はアプリ定義）。</summary>
        private readonly int _refId;

        /// <summary>配置規則。</summary>
        private readonly LandmarkRule _rule;

        /// <summary>RegionCenter のとき、区画をバイオームで絞る（None=絞らない）。</summary>
        private readonly BiomeId _regionBiomeFilter;

        /// <summary>LandmarkPlacementPass を生成する。</summary>
        public LandmarkPlacementPass(PlacementKind kind, int refId, LandmarkRule rule,
            BiomeId regionBiomeFilter = default)
        {
            _kind = kind;
            _refId = refId;
            _rule = rule;
            _regionBiomeFilter = regionBiomeFilter;
        }

        /// <summary>パス名。</summary>
        public string Name => $"Landmark({_kind})";

        /// <summary>規則どおりに1点置く（置けない構成なら静かに諦める）。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            switch (_rule)
            {
                case LandmarkRule.Farthest:
                    PlaceFarthest(blueprint);
                    break;
                case LandmarkRule.RandomWalkable:
                    PlaceRandomWalkable(blueprint, random);
                    break;
                case LandmarkRule.RegionCenter:
                    PlaceRegionCenter(blueprint, random);
                    break;
            }
        }

        /// <summary>起点から最遠の歩行可能・未占有セルに置く。</summary>
        private void PlaceFarthest(StageBlueprint blueprint)
        {
            var grid = blueprint.Grid;
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
            var bestDistance = -1;
            var bestX = -1;
            var bestY = -1;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var distance = distances[y * grid.Width + x];
                    if (distance > bestDistance && !blueprint.IsOccupied(x, y))
                    {
                        bestDistance = distance;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            if (bestDistance >= 0)
            {
                blueprint.AddPlacement(_kind, _refId, bestX, bestY);
            }
        }

        /// <summary>到達可能な歩行可能セルから乱択で置く。</summary>
        private void PlaceRandomWalkable(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            if (!grid.TryFindFirstWalkable(out var startX, out var startY))
            {
                return;
            }
            if (blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var spawn))
            {
                startX = spawn.X;
                startY = spawn.Y;
            }
            var distances = grid.ComputeDistances(startX, startY);

            var candidates = new System.Collections.Generic.List<(int X, int Y)>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    // 到達可能（起点以外）かつ未占有だけが候補
                    if (distances[y * grid.Width + x] > 0 && !blueprint.IsOccupied(x, y))
                    {
                        candidates.Add((x, y));
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }
            var (px, py) = candidates[random.NextInt(candidates.Count)];
            blueprint.AddPlacement(_kind, _refId, px, py);
        }

        /// <summary>区画の中心近傍（歩行可能セルへ螺旋探索）に置く。</summary>
        private void PlaceRegionCenter(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;

            // バイオームフィルタに合う区画を集める（Noneなら全区画）
            var candidates = new System.Collections.Generic.List<GridRect>();
            for (var i = 0; i < blueprint.Regions.Count; i++)
            {
                var region = blueprint.Regions[i];
                if (_regionBiomeFilter.Equals(BiomeId.None)
                    || grid.GetBiome(region.CenterX, region.CenterY).Equals(_regionBiomeFilter))
                {
                    candidates.Add(region);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            var target = candidates[random.NextInt(candidates.Count)];
            // 中心から外へ向けて歩行可能・未占有セルを探す（半径を広げる矩形リング走査）
            for (var radius = 0; radius < System.Math.Max(target.Width, target.Height); radius++)
            {
                for (var y = target.CenterY - radius; y <= target.CenterY + radius; y++)
                {
                    for (var x = target.CenterX - radius; x <= target.CenterX + radius; x++)
                    {
                        if (!grid.InBounds(x, y) || !grid.Get(x, y).IsWalkable
                            || blueprint.IsOccupied(x, y))
                        {
                            continue;
                        }
                        blueprint.AddPlacement(_kind, _refId, x, y);
                        return;
                    }
                }
            }
        }
    }
}
