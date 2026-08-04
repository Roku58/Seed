using Seed.Hub.Contracts;

namespace Game.Battle.Contracts
{
    /// <summary>
    /// キャラクターがダメージを受けたという出来事（通知・過去形）。
    /// 値はスナップショット（発生時点の残りHP）で自己完結させる。
    /// </summary>
    public readonly struct CharacterDamaged : INotificationMessage
    {
        /// <summary>受けた側。</summary>
        public readonly CharacterId Target;

        /// <summary>ダメージ量。</summary>
        public readonly int Damage;

        /// <summary>発生時点の残りHP。</summary>
        public readonly int RemainingHp;

        /// <summary>最大HP（HPバー表示用）。</summary>
        public readonly int MaxHp;

        /// <summary>CharacterDamaged を生成する。</summary>
        public CharacterDamaged(CharacterId target, int damage, int remainingHp, int maxHp)
        {
            Target = target;
            Damage = damage;
            RemainingHp = remainingHp;
            MaxHp = maxHp;
        }
    }
}
