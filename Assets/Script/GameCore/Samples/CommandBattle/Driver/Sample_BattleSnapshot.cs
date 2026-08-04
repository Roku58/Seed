namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】バトル全体のスナップショット（★パターン: 巻き戻し）。
    /// コア側（時刻・乱数）は CoreSnapshot、ゲーム側（HP・コンディション・ターン数）と
    /// レコード件数は自前で控える。復元はこの逆をなぞるだけ。
    /// </summary>
    public readonly struct Sample_BattleSnapshot
    {
        /// <summary>コア側（時刻・乱数）のスナップショット。</summary>
        public readonly CoreSnapshot Core;
        /// <summary>購読状態のスナップショット。</summary>
        public readonly SubscriptionSnapshot Subscriptions;
        /// <summary>アクターAのスナップショット。</summary>
        public readonly Sample_ActorSnapshot ActorA;
        /// <summary>アクターBのスナップショット。</summary>
        public readonly Sample_ActorSnapshot ActorB;
        /// <summary>ターン番号。</summary>
        public readonly int Turn;
        /// <summary>レコード件数の控え。</summary>
        public readonly int RecordCount;

        /// <summary>Sample_BattleSnapshot を生成する。</summary>
        private Sample_BattleSnapshot(CoreSnapshot core, SubscriptionSnapshot subscriptions,
            Sample_ActorSnapshot a, Sample_ActorSnapshot b, int turn, int recordCount)
        {
            Core = core;
            Subscriptions = subscriptions;
            ActorA = a;
            ActorB = b;
            Turn = turn;
            RecordCount = recordCount;
        }

        /// <summary>バトル全体の状態を保存する。</summary>
        public static Sample_BattleSnapshot Capture(LogicContext ctx, Sample_Actor a, Sample_Actor b)
        {
            return new Sample_BattleSnapshot(
                ctx.CaptureCoreSnapshot(),
                ctx.Hub.CaptureSnapshot(), // ★購読も保存（きのみ消費・まもる登録も巻き戻る）
                a.CaptureSnapshot(),
                b.CaptureSnapshot(),
                Sample_CommandContext.Field(ctx).Turn,
                Sample_CommandContext.Log(ctx).Count);
        }

        /// <summary>保存した状態へ丸ごと巻き戻す。</summary>
        public static void Restore(LogicContext ctx, Sample_Actor a, Sample_Actor b,
            in Sample_BattleSnapshot snapshot)
        {
            ctx.RestoreCoreSnapshot(in snapshot.Core);
            ctx.Hub.RestoreSnapshot(snapshot.Subscriptions);
            a.RestoreSnapshot(in snapshot.ActorA);
            b.RestoreSnapshot(in snapshot.ActorB);
            Sample_CommandContext.Field(ctx).Turn = snapshot.Turn;
            Sample_CommandContext.Log(ctx).TruncateTo(snapshot.RecordCount);
        }
    }
}
