using System;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】バトル参加者の基底（ハンター・モンスター共通部分）。</summary>
    public abstract class Sample_Unit : Sample_IUnitWriter
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>最大HP。</summary>
        public int MaxHp { get; }
        /// <summary>現在HP。</summary>
        public int Hp { get; private set; }
        /// <summary>保持中のコンディション集合。</summary>
        public ConditionSet<Sample_ConditionKind> Conditions { get; } = new ConditionSet<Sample_ConditionKind>();
        /// <summary>戦闘不能か。</summary>
        public bool IsDead => Hp <= 0;

        /// <summary>Sample_Unit を生成する。</summary>
        protected Sample_Unit(string name, int maxHp)
        {
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>ダメージ分だけHPを減らす（下限0）。</summary>
        void Sample_IUnitWriter.ApplyDamage(int damage) => Hp = Math.Max(0, Hp - damage);
        /// <summary>スタミナを消費する（下限0）。</summary>
        void Sample_IUnitWriter.ConsumeStamina(int amount) => OnConsumeStamina(amount);
        /// <summary>スタミナを回復する（上限あり）。</summary>
        void Sample_IUnitWriter.RegenStamina(int amount) => OnRegenStamina(amount);

        /// <summary>重ねがけポリシーに従いコンディションを付与する。</summary>
        bool Sample_IUnitWriter.AddConditionMerged(in TimedCondition<Sample_ConditionKind> condition,
            ConditionMergePolicy policy, long nowMs)
        {
            /// <summary>重ねがけポリシー付きの付与。</summary>
            return Conditions.AddOrMerge(in condition, policy, nowMs);
        }

        /// <summary>スタミナ消費の派生先フック（既定では何もしない）。</summary>
        protected virtual void OnConsumeStamina(int amount)
        {
        }
        /// <summary>スタミナ回復の派生先フック（既定では何もしない）。</summary>
        protected virtual void OnRegenStamina(int amount)
        {
        }
    }
}
