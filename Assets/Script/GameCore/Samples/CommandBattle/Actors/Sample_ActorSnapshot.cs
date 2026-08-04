namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】アクター状態のスナップショット（巻き戻しパターンの一部）。</summary>
    public readonly struct Sample_ActorSnapshot
    {
        /// <summary>現在HP。</summary>
        public readonly int Hp;
        public readonly ConditionSet<Sample_ConditionKind> Conditions; // クローン済みの集合

        /// <summary>Sample_ActorSnapshot を生成する。</summary>
        public Sample_ActorSnapshot(int hp, ConditionSet<Sample_ConditionKind> conditions)
        {
            Hp = hp;
            Conditions = conditions;
        }
    }
}
