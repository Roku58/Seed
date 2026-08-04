namespace Seed.Core
{
    /// <summary>
    /// 世界まるごとのスナップショット（★巻き戻しの一元化）。
    ///
    /// コア状態（時刻・乱数・暴走ガードのカウンタ）＋ EventHub の購読状態＋
    /// 全 ISnapshotParticipant の状態を1個のトークンに束ねたもの。
    /// LogicContext.CaptureAll で取得し、RestoreAll で戻す。
    ///
    /// [なぜ束ねるか] 4系統（コア・購読・レコード件数・ゲーム状態）を呼び出し側が
    /// 手で並べる方式は「1つ忘れても動いてしまう」ため、忘れた分だけ静かにズレる。
    /// 束ねておけば取り違え・呼び漏れが構造的に起きない。
    ///
    /// 中身は読み取り専用。フィールドは診断用に公開するが、
    /// 参加者の状態は不透明トークンなので解釈しないこと。
    /// </summary>
    public sealed class WorldSnapshot
    {
        /// <summary>コア側（時刻・乱数・暴走ガードのカウンタ）のスナップショット。</summary>
        public readonly CoreSnapshot Core;

        /// <summary>購読状態のスナップショット。</summary>
        public readonly SubscriptionSnapshot Subscriptions;

        /// <summary>参加者の状態（登録順。要素は各参加者の不透明トークン）。</summary>
        internal readonly object[] ParticipantStates;

        /// <summary>参加者の件数（診断・アサート用）。</summary>
        public int ParticipantCount => ParticipantStates.Length;

        /// <summary>WorldSnapshot を生成する（生成は LogicContext.CaptureAll の責務）。</summary>
        internal WorldSnapshot(in CoreSnapshot core, SubscriptionSnapshot subscriptions,
            object[] participantStates)
        {
            Core = core;
            Subscriptions = subscriptions;
            ParticipantStates = participantStates;
        }
    }
}
