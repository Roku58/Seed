namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】部位耐久減少セクションの入力。</summary>
    public readonly struct Sample_PartDamageInput
    {
        /// <summary>対象部位。</summary>
        public readonly Sample_MonsterPart Part;
        /// <summary>量。</summary>
        public readonly int Amount;

        /// <summary>Sample_PartDamageInput を生成する。</summary>
        public Sample_PartDamageInput(Sample_MonsterPart part, int amount)
        {
            Part = part;
            Amount = amount;
        }
    }
}
