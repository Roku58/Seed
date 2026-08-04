using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 素材バリアントの抽選を設計図へ焼き込む（床や壁などの複数素材登録の生成側）。
    ///
    /// セルごとに (バイオーム, セル種別) の重み表からバリアント番号を選び、
    /// バリアント層へ記録する。**抽選を生成側（純C#）で済ませる**のが要点——
    /// 施工側で乱数を引くと「同じシードなのに見た目が違う」が起き、
    /// リプレイ・検証・ロックステップと矛盾するため。
    /// 走査順は固定（y→x）で決定的。
    /// </summary>
    public sealed class TileVariantPass : IGenerationPass
    {
        /// <summary>バリアントの重み表。</summary>
        private readonly TileVariantTable _table;

        /// <summary>TileVariantPass を生成する。</summary>
        public TileVariantPass(TileVariantTable table)
        {
            _table = table ?? new TileVariantTable();
        }

        /// <summary>パス名。</summary>
        public string Name => "TileVariant";

        /// <summary>全セルのバリアントを抽選する。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = grid.Get(x, y);
                    if (cell.Equals(CellType.None))
                    {
                        continue;
                    }
                    grid.SetVariant(x, y, _table.Pick(grid.GetBiome(x, y), cell, random));
                }
            }
        }
    }
}
