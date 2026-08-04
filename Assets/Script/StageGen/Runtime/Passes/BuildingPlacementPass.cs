using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 区画へ建物を置く（街の中身）。
    ///
    /// 各区画（Blueprint.Regions）の内側に、外周1セルの歩道を残して乱数サイズの
    /// 建物ブロック（Building）を置き、道路側の辺へ出入口（Door）を1つ開ける。
    /// 歩道が必ず残るため、建物を置いても歩行可能領域の連結は壊れない。
    /// </summary>
    public sealed class BuildingPlacementPass : IGenerationPass
    {
        /// <summary>建物の最小辺長。</summary>
        private const int MinBuildingSize = 2;

        /// <summary>パス名。</summary>
        public string Name => "BuildingPlacement";

        /// <summary>全区画へ建物を置く。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            for (var i = 0; i < blueprint.Regions.Count; i++)
            {
                var region = blueprint.Regions[i];

                // 歩道1セルを残した内側が建物の最大枠
                var maxWidth = region.Width - 2;
                var maxHeight = region.Height - 2;
                if (maxWidth < MinBuildingSize || maxHeight < MinBuildingSize)
                {
                    continue; // 建物が入らない小区画は広場のまま
                }

                var width = MinBuildingSize + random.NextInt(maxWidth - MinBuildingSize + 1);
                var height = MinBuildingSize + random.NextInt(maxHeight - MinBuildingSize + 1);
                var x = region.X + 1 + random.NextInt(maxWidth - width + 1);
                var y = region.Y + 1 + random.NextInt(maxHeight - height + 1);

                for (var yy = y; yy < y + height; yy++)
                {
                    for (var xx = x; xx < x + width; xx++)
                    {
                        grid.Set(xx, yy, CellType.Building);
                    }
                }

                // 出入口: 建物周囲の4辺から1辺を選び、中央に Door を開ける
                // （どの辺の外も歩道なので、必ず歩行可能領域へ繋がる）
                var side = random.NextInt(4);
                switch (side)
                {
                    case 0:
                        grid.Set(x + width / 2, y, CellType.Door);
                        break;
                    case 1:
                        grid.Set(x + width / 2, y + height - 1, CellType.Door);
                        break;
                    case 2:
                        grid.Set(x, y + height / 2, CellType.Door);
                        break;
                    default:
                        grid.Set(x + width - 1, y + height / 2, CellType.Door);
                        break;
                }
            }
        }
    }
}
