namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】状態書き込み用インターフェース（★パターン: 書き込み口の分離）。
    /// internal setter の代わりに書き込み口を分離し、セクション/ハンドラーだけが
    /// キャストして使う。asmdefをどう分割しても壊れない。
    /// </summary>
    public interface Sample_IActorWriter
    {
        void ApplyDamage(int damage);

        /// <summary>重ねがけポリシー付きのコンディション付与（★パターン: ConditionMergePolicy）。</summary>
        bool AddConditionMerged(in TimedCondition<Sample_ConditionKind> condition,
            ConditionMergePolicy policy, long nowMs);

        void CureCondition(Sample_ConditionKind kind);
    }
}
