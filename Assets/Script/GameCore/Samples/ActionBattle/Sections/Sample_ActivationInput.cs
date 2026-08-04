namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】発動判定セクションの入力。</summary>
    public readonly struct Sample_ActivationInput
    {
        /// <summary>行動主体。</summary>
        public readonly Sample_Unit Actor;
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_AttackMove Move;

        /// <summary>Sample_ActivationInput を生成する。</summary>
        public Sample_ActivationInput(Sample_Unit actor, Sample_AttackMove move)
        {
            Actor = actor;
            Move = move;
        }
    }
}
