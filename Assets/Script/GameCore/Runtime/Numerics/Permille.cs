namespace Seed.Core
{
    /// <summary>
    /// 整数パーミル（‰ = 1/1000）演算。
    ///
    /// [設計原則: 決定性]
    /// ロジックの計算に float を使うと、プラットフォーム・最適化・演算順序の差で
    /// 結果が揺れ、リプレイ・通信同期・ゴールデンログテストが成立しなくなる。
    /// 本基盤の計算はすべて整数で行い、倍率は ‰ の整数で表現する。
    /// 例: x1.25 → 1250 / x0.9 → 900 / 10% → 100
    /// </summary>
    public static class Permille
    {
        /// <summary>等倍(1000‰)。</summary>
        public const int One = 1000;

        /// <summary>value × (permille/1000)。long 経由でオーバーフローを回避する。</summary>
        public static int Apply(int value, int permille)
        {
            return (int)((long)value * permille / One);
        }
    }
}
