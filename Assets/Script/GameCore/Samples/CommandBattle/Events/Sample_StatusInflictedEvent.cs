namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】状態異常付与の通知イベント（クラボのみ等の「連鎖」の起点）。</summary>
    public sealed class Sample_StatusInflictedEvent : LogicEvent
    {
        /// <summary>対象。</summary>
        public Sample_Actor Target;
        /// <summary>コンディション種別。</summary>
        public Sample_ConditionKind Condition;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Target = null;
            Condition = Sample_ConditionKind.None;
        }
    }
}
