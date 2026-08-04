using Seed.Hub.Contracts;

namespace Game.Battle.Contracts
{
    /// <summary>ガード入力の変化（押した/離した）の通知。</summary>
    public readonly struct GuardInputChanged : INotificationMessage
    {
        /// <summary>対象キャラクター。</summary>
        public readonly CharacterId Character;

        /// <summary>ガード姿勢に入ったか。</summary>
        public readonly bool IsGuarding;

        /// <summary>GuardInputChanged を生成する。</summary>
        public GuardInputChanged(CharacterId character, bool isGuarding)
        {
            Character = character;
            IsGuarding = isGuarding;
        }
    }
}
