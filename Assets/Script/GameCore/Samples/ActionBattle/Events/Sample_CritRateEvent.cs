namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】会心率補正イベント（弱点特効などの介入点）。</summary>
    public sealed class Sample_CritRateEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Unit Attacker;
        /// <summary>防御側。</summary>
        public Sample_Unit Defender;
        /// <summary>被弾部位の肉質(%)。</summary>
        public int HitZonePercent;
        /// <summary>会心率(‰)。</summary>
        public int RatePermille;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Defender = null;
            HitZonePercent = 0;
            RatePermille = 0;
        }
    }
}
