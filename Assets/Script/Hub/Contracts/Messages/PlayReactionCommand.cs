namespace Seed.Hub.Contracts
{
    /// <summary>
    /// キャラクターへリアクション再生を依頼する命令（処理者はキャラクター基盤のみ）。
    /// 「どんな出来事のときに何を再生するか」の方針は発行側（アプリ）が持つ。
    /// </summary>
    public readonly struct PlayReactionCommand : ICommandMessage
    {
        /// <summary>対象キャラクター。</summary>
        public readonly CharacterId Target;

        /// <summary>再生するリアクション。</summary>
        public readonly ReactionId Reaction;

        /// <summary>リアクションに添える荷物（強度・方向など。意味はアプリ定義）。</summary>
        public readonly int Payload;

        /// <summary>PlayReactionCommand を生成する。</summary>
        public PlayReactionCommand(CharacterId target, ReactionId reaction, int payload = 0)
        {
            Target = target;
            Reaction = reaction;
            Payload = payload;
        }
    }
}
