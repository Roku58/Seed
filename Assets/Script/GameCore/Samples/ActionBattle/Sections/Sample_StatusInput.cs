namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】状態異常蓄積セクションの入力。</summary>
    public readonly struct Sample_StatusInput
    {
        /// <summary>対象。</summary>
        public readonly Sample_Monster Target;
        /// <summary>対象部位。</summary>
        public readonly Sample_MonsterPart Part;
        /// <summary>状態異常種別。</summary>
        public readonly Sample_StatusKind Status;
        /// <summary>量。</summary>
        public readonly int Amount;

        /// <summary>Sample_StatusInput を生成する。</summary>
        public Sample_StatusInput(Sample_Monster target, Sample_MonsterPart part,
            Sample_StatusKind status, int amount)
        {
            Target = target;
            Part = part;
            Status = status;
            Amount = amount;
        }
    }
}
