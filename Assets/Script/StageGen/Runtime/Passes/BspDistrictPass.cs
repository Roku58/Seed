using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// BSP区画割り＋道路（街の骨格）。
    ///
    /// アルゴリズム: 領域を再帰的に2分割する。分割位置は乱数（両側が最小サイズを
    /// 下回らない範囲）、分割方向は長辺優先。**分割線そのものを道路（Road）として引く**ため、
    /// 道路網は構造上すべて連結する。最小サイズまで割れなくなった葉が「区画」になり、
    /// Blueprint.Regions へ記録される（建物配置・ショップ配置・バイオーム塗りが後で使う）。
    /// </summary>
    public sealed class BspDistrictPass : IGenerationPass
    {
        /// <summary>区画の最小辺長（これ未満には割らない）。</summary>
        private readonly int _minDistrictSize;

        /// <summary>BspDistrictPass を生成する。</summary>
        public BspDistrictPass(int minDistrictSize = 7)
        {
            _minDistrictSize = minDistrictSize < 3 ? 3 : minDistrictSize;
        }

        /// <summary>パス名。</summary>
        public string Name => "BspDistrict";

        /// <summary>区画割りを実行する（外周1セルは道路の環）。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;

            // 外周は道路の環にする（どの区画からも外周へ抜けられる）
            for (var x = 0; x < grid.Width; x++)
            {
                grid.Set(x, 0, CellType.Road);
                grid.Set(x, grid.Height - 1, CellType.Road);
            }
            for (var y = 0; y < grid.Height; y++)
            {
                grid.Set(0, y, CellType.Road);
                grid.Set(grid.Width - 1, y, CellType.Road);
            }

            Split(blueprint, new GridRect(1, 1, grid.Width - 2, grid.Height - 2), random);
        }

        /// <summary>再帰分割（割れなくなったら区画として記録）。</summary>
        private void Split(StageBlueprint blueprint, GridRect area, DeterministicRandom random)
        {
            // 分割すると両側が最小サイズを保てるか（道路1セルぶんを差し引く）
            var canSplitX = area.Width >= _minDistrictSize * 2 + 1;
            var canSplitY = area.Height >= _minDistrictSize * 2 + 1;
            if (!canSplitX && !canSplitY)
            {
                blueprint.Regions.Add(area);
                return;
            }

            // 長辺優先（同じなら乱数）で分割方向を決める
            bool splitVertical;
            if (canSplitX && canSplitY)
            {
                splitVertical = area.Width != area.Height
                    ? area.Width > area.Height
                    : random.NextInt(2) == 0;
            }
            else
            {
                splitVertical = canSplitX;
            }

            var grid = blueprint.Grid;
            if (splitVertical)
            {
                // 縦の道路（X位置を乱数で決める）
                var roadX = area.X + _minDistrictSize
                    + random.NextInt(area.Width - _minDistrictSize * 2);
                for (var y = area.Y - 1; y <= area.YMax; y++)
                {
                    grid.Set(roadX, y, CellType.Road); // 親の道路まで届かせて必ず接続する
                }
                Split(blueprint, new GridRect(area.X, area.Y, roadX - area.X, area.Height), random);
                Split(blueprint, new GridRect(roadX + 1, area.Y, area.XMax - roadX - 1, area.Height), random);
            }
            else
            {
                var roadY = area.Y + _minDistrictSize
                    + random.NextInt(area.Height - _minDistrictSize * 2);
                for (var x = area.X - 1; x <= area.XMax; x++)
                {
                    grid.Set(x, roadY, CellType.Road);
                }
                Split(blueprint, new GridRect(area.X, area.Y, area.Width, roadY - area.Y), random);
                Split(blueprint, new GridRect(area.X, roadY + 1, area.Width, area.YMax - roadY - 1), random);
            }
        }
    }
}
