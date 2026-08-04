namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】状態異常蓄積補正イベント（状態異常強化スキル・モンスター耐性の介入点）。</summary>
    public sealed class Sample_StatusBuildupEvent : LogicEvent
    {
        /// <summary>対象。</summary>
        public Sample_Unit Target;
        /// <summary>状態異常種別。</summary>
        public Sample_StatusKind Status;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public int Value;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Target = null;
            Status = Sample_StatusKind.None;
            Value = 0;
        }
    }
}
