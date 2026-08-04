using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 素材バリアントの重み表（(バイオーム, セル種別) → バリアント番号ごとの重み）。
    ///
    /// 「床や壁などの複数素材登録」の純データ側——どの見た目バリアントを使うかの抽選規則。
    /// 実際のプレハブ/色は施工側の StageAssetPalette が同じ (バイオーム, セル, バリアント) キーで持つ。
    /// 表に無い組み合わせはバリアント0（既定素材）になる。
    /// </summary>
    public sealed class TileVariantTable
    {
        /// <summary>(biome, cell) → 重み配列（index=バリアント番号）。</summary>
        private readonly Dictionary<(int Biome, int Cell), int[]> _weights =
            new Dictionary<(int, int), int[]>();

        /// <summary>重み行を登録する（weights[i] = バリアントiの相対重み）。流れるように書ける糖衣。</summary>
        public TileVariantTable Add(BiomeId biome, CellType cell, params int[] weights)
        {
            _weights[(biome.Value, cell.Value)] = weights;
            return this;
        }

        /// <summary>
        /// バリアントを抽選する。
        /// 検索は (バイオーム, セル) → (None, セル) → 無ければ 0（既定素材）。
        /// 乱数消費を一定に保つため、表に行が無い場合は乱数を消費しない。
        /// </summary>
        public int Pick(BiomeId biome, CellType cell, DeterministicRandom random)
        {
            if (!_weights.TryGetValue((biome.Value, cell.Value), out var weights)
                && !_weights.TryGetValue((BiomeId.None.Value, cell.Value), out weights))
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0)
                {
                    total += weights[i];
                }
            }
            if (total <= 0)
            {
                return 0;
            }

            var roll = random.NextInt(total);
            for (var i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0)
                {
                    continue; // 重み0は「絶対に出ない」
                }
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
