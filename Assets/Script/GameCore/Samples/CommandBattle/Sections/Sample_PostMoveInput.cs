namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】技効果後処理セクションの入力。</summary>
    public readonly struct Sample_PostMoveInput
    {
        /// <summary>技の使用者。</summary>
        public readonly Sample_Actor User;
        /// <summary>対象。</summary>
        public readonly Sample_Actor Target;
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_MoveData Move;
        /// <summary>与えたダメージ。</summary>
        public readonly int DamageDealt;

        /// <summary>Sample_PostMoveInput を生成する。</summary>
        public Sample_PostMoveInput(Sample_Actor user, Sample_Actor target, Sample_MoveData move, int damageDealt)
        {
            User = user;
            Target = target;
            Move = move;
            DamageDealt = damageDealt;
        }
    }
}
