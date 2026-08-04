using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// 「あるキャラクターの行動が始まった」という基盤→外向けの出来事（C#イベント引数）。
    /// 基盤自身は Hub へ発行しない——この出来事をどのメッセージへ変換するか
    /// （例: Attack 開始 → AttackRequested 発行）はアプリ側の方針。
    /// </summary>
    public readonly struct CharacterBehaviorEvent
    {
        /// <summary>行動を始めたキャラクター。</summary>
        public readonly CharacterId Id;

        /// <summary>始まった行動。</summary>
        public readonly BehaviorKey Behavior;

        /// <summary>行動に添えられた荷物（技IDなど）。</summary>
        public readonly int Payload;

        /// <summary>CharacterBehaviorEvent を生成する。</summary>
        public CharacterBehaviorEvent(CharacterId id, BehaviorKey behavior, int payload)
        {
            Id = id;
            Behavior = behavior;
            Payload = payload;
        }
    }
}
