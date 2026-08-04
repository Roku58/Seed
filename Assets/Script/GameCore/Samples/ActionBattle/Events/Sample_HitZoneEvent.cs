namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】肉質補正イベント（傷・軟化などの介入点）。</summary>
    public sealed class Sample_HitZoneEvent : LogicEvent
    {
        /// <summary>防御側。</summary>
        public Sample_Unit Defender;
        /// <summary>対象部位。</summary>
        public Sample_MonsterPart Part;
        /// <summary>肉質(%)。</summary>
        public int Percent;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Defender = null;
            Part = null;
            Percent = 0;
        }
    }
}
