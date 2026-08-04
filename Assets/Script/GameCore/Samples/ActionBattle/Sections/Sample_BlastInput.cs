namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】爆破ダメージセクションの入力。</summary>
    public readonly struct Sample_BlastInput
    {
        /// <summary>対象。</summary>
        public readonly Sample_Monster Target;
        /// <summary>対象部位。</summary>
        public readonly Sample_MonsterPart Part;

        /// <summary>Sample_BlastInput を生成する。</summary>
        public Sample_BlastInput(Sample_Monster target, Sample_MonsterPart part)
        {
            Target = target;
            Part = part;
        }
    }
}
