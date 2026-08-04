using Seed.Hub.Contracts;

namespace Game.Battle.Contracts
{
    /// <summary>キャラクターが戦闘不能になったという出来事（通知・過去形）。</summary>
    public readonly struct CharacterDied : INotificationMessage
    {
        /// <summary>倒れたキャラクター。</summary>
        public readonly CharacterId Target;

        /// <summary>CharacterDied を生成する。</summary>
        public CharacterDied(CharacterId target)
        {
            Target = target;
        }
    }
}
