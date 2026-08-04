using System.Collections.Generic;

namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】タイプ相性表（‰）。未登録の組み合わせは等倍(1000)。</summary>
    public static class Sample_TypeChart
    {
        private static readonly Dictionary<(Sample_Element attack, Sample_Element defend), int> Table =
            new Dictionary<(Sample_Element, Sample_Element), int>
            {
                { (Sample_Element.Fire, Sample_Element.Grass), 2000 },
                { (Sample_Element.Fire, Sample_Element.Water), 500 },
                { (Sample_Element.Fire, Sample_Element.Fire), 500 },
                { (Sample_Element.Water, Sample_Element.Fire), 2000 },
                { (Sample_Element.Electric, Sample_Element.Water), 2000 },
                { (Sample_Element.Electric, Sample_Element.Flying), 2000 },
                { (Sample_Element.Electric, Sample_Element.Grass), 500 },
                { (Sample_Element.Electric, Sample_Element.Electric), 500 },
            };

        /// <summary>攻撃タイプ×防御タイプ群の相性倍率(‰)を返す。</summary>
        public static int GetPermille(Sample_Element attack, IReadOnlyList<Sample_Element> defenderTypes)
        {
            var permille = Permille.One;
            for (var i = 0; i < defenderTypes.Count; i++)
            {
                if (Table.TryGetValue((attack, defenderTypes[i]), out var p))
                {
                    permille = permille * p / Permille.One;
                }
            }
            return permille;
        }
    }
}
