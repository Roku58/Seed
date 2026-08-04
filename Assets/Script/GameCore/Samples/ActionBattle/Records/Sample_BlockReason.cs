namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】発動判定で弾かれた理由。SectionResult の失敗コードにも使う。</summary>
    public enum Sample_BlockReason
    {
        /// <summary>なし。</summary>
        None,
        /// <summary>スタミナ不足。</summary>
        Stamina,
        /// <summary>まひ・拘束。</summary>
        Paralyzed,
        /// <summary>個別仕様による阻止。</summary>
        Handler,
        /// <summary>戦闘不能。</summary>
        Dead,
    }
}
