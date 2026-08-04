namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】状態書き込み用インターフェース（★パターン: 書き込み口の分離）。</summary>
    public interface Sample_IUnitWriter
    {
        void ApplyDamage(int damage);
        void ConsumeStamina(int amount);
        void RegenStamina(int amount);

        /// <summary>重ねがけポリシー付きのコンディション付与（★パターン: ConditionMergePolicy）。</summary>
        bool AddConditionMerged(in TimedCondition<Sample_ConditionKind> condition,
            ConditionMergePolicy policy, long nowMs);
    }
}
