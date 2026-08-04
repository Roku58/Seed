namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】状態異常付与セクションの入力。</summary>
    public readonly struct Sample_StatusInput
    {
        /// <summary>対象。</summary>
        public readonly Sample_Actor Target;
        /// <summary>コンディション種別。</summary>
        public readonly Sample_ConditionKind Condition;

        /// <summary>Sample_StatusInput を生成する。</summary>
        public Sample_StatusInput(Sample_Actor target, Sample_ConditionKind condition)
        {
            Target = target;
            Condition = condition;
        }
    }
}
