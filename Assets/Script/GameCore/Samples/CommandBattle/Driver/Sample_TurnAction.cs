namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】ターン制の入力：「誰が・誰に・何をするか」。</summary>
    public readonly struct Sample_TurnAction
    {
        /// <summary>技の使用者。</summary>
        public readonly Sample_Actor User;
        /// <summary>対象。</summary>
        public readonly Sample_Actor Target;
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_MoveData Move;

        /// <summary>Sample_TurnAction を生成する。</summary>
        public Sample_TurnAction(Sample_Actor user, Sample_Actor target, Sample_MoveData move)
        {
            User = user;
            Target = target;
            Move = move;
        }
    }
}
