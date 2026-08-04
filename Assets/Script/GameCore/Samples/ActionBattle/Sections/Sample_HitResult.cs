namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】ヒット解決の結果。</summary>
    public readonly struct Sample_HitResult
    {
        /// <summary>連鎖（爆破等）も含む合計ダメージ</summary>
        public readonly int TotalDamage;

        /// <summary>Sample_HitResult を生成する。</summary>
        public Sample_HitResult(int totalDamage)
        {
            TotalDamage = totalDamage;
        }
    }
}
