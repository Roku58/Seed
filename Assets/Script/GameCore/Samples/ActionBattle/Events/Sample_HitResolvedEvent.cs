namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】1ヒットの解決完了リアクション（怒り蓄積・カウンター等の割り込み起点）。</summary>
    public sealed class Sample_HitResolvedEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Unit Attacker;
        /// <summary>防御側。</summary>
        public Sample_Unit Defender;
        /// <summary>連鎖も含む合計ダメージ。</summary>
        public int TotalDamage;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Defender = null;
            TotalDamage = 0;
        }
    }
}
