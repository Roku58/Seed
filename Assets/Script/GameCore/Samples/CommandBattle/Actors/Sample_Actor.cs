using System;
using System.Collections.Generic;

namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】バトル参加者。可変状態（HP・コンディション）はすべてここに集約する。</summary>
    public sealed class Sample_Actor : Sample_IActorWriter
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>タイプ一覧。</summary>
        public IReadOnlyList<Sample_Element> Types { get; }
        /// <summary>最大HP。</summary>
        public int MaxHp { get; }
        /// <summary>攻撃力。</summary>
        public int Attack { get; }
        /// <summary>防御力。</summary>
        public int Defense { get; }
        /// <summary>素早さ。</summary>
        public int Speed { get; }
        /// <summary>現在HP。</summary>
        public int Hp { get; private set; }
        /// <summary>保持中のコンディション集合。</summary>
        public ConditionSet<Sample_ConditionKind> Conditions { get; } = new ConditionSet<Sample_ConditionKind>();
        /// <summary>ひんし状態か。</summary>
        public bool IsFainted => Hp <= 0;

        /// <summary>Sample_Actor を生成する。</summary>
        public Sample_Actor(string name, Sample_Element[] types, int maxHp, int attack, int defense, int speed)
        {
            Name = name;
            Types = types;
            MaxHp = maxHp;
            Hp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
        }

        /// <summary>指定タイプを持っているかを返す。</summary>
        public bool HasType(Sample_Element element)
        {
            for (var i = 0; i < Types.Count; i++)
            {
                if (Types[i] == element)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- 巻き戻し用スナップショット（★パターン: CoreSnapshotとペアで使う） ----

        /// <summary>巻き戻し用スナップショットを取得する。</summary>
        public Sample_ActorSnapshot CaptureSnapshot()
        {
            var conditions = new ConditionSet<Sample_ConditionKind>();
            Conditions.CopyTo(conditions);
            return new Sample_ActorSnapshot(Hp, conditions);
        }

        /// <summary>スナップショットから状態を復元する。</summary>
        public void RestoreSnapshot(in Sample_ActorSnapshot snapshot)
        {
            Hp = snapshot.Hp;
            snapshot.Conditions.CopyTo(Conditions);
        }

        // ---- 書き込みは Sample_IActorWriter 経由のみ（明示的実装で隠す） ----

        /// <summary>ダメージ分だけHPを減らす（下限0）。</summary>
        void Sample_IActorWriter.ApplyDamage(int damage)
        {
            Hp = Math.Max(0, Hp - damage);
        }

        /// <summary>重ねがけポリシーに従いコンディションを付与する。</summary>
        bool Sample_IActorWriter.AddConditionMerged(in TimedCondition<Sample_ConditionKind> condition,
            ConditionMergePolicy policy, long nowMs)
        {
            /// <summary>重ねがけポリシー付きの付与。</summary>
            return Conditions.AddOrMerge(in condition, policy, nowMs);
        }

        /// <summary>指定種別のコンディションを取り除く。</summary>
        void Sample_IActorWriter.CureCondition(Sample_ConditionKind kind)
        {
            Conditions.Remove(kind);
        }
    }
}
