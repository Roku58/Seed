namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】技実行系セクションの入力（readonly struct で受け渡す規約）。</summary>
    public readonly struct Sample_MoveInput
    {
        /// <summary>技の使用者。</summary>
        public readonly Sample_Actor User;
        /// <summary>対象。</summary>
        public readonly Sample_Actor Target;
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_MoveData Move;

        /// <summary>Sample_MoveInput を生成する。</summary>
        public Sample_MoveInput(Sample_Actor user, Sample_Actor target, Sample_MoveData move)
        {
            User = user;
            Target = target;
            Move = move;
        }
    }
}
