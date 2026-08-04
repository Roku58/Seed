namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】発動失敗の理由。SectionResult の失敗コードとしても使う（intキャスト）。</summary>
    public enum Sample_FailReason
    {
        /// <summary>なし。</summary>
        None,
        /// <summary>まひ・拘束。</summary>
        Paralyzed,
        /// <summary>まもる等による阻止。</summary>
        Blocked,
        /// <summary>ひんしで行動不可。</summary>
        Fainted,
    }
}
