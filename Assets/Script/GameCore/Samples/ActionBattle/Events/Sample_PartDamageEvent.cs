namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】部位蓄積補正イベント（破壊王などの介入点）。</summary>
    public sealed class Sample_PartDamageEvent : LogicEvent
    {
        /// <summary>対象部位。</summary>
        public Sample_MonsterPart Part;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public int Value;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Part = null;
            Value = 0;
        }
    }
}
