namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】発動判定イベント。Failed にすると技が失敗する（まもる等の介入点）。</summary>
    public sealed class Sample_ActivationCheckEvent : LogicEvent
    {
        /// <summary>技の使用者。</summary>
        public Sample_Actor User;
        /// <summary>対象。</summary>
        public Sample_Actor Target;
        /// <summary>使用モーション/技。</summary>
        public Sample_MoveData Move;
        /// <summary>true なら発動失敗。</summary>
        public bool Failed;
        /// <summary>失敗理由。</summary>
        public Sample_FailReason Reason;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            User = null;
            Target = null;
            Move = null;
            Failed = false;
            Reason = Sample_FailReason.None;
        }
    }
}
