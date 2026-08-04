namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】最終ダメージ補正イベント（ガードのチップ化・怒りの被ダメ軽減が介入）。</summary>
    public sealed class Sample_FinalDamageEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Unit Attacker;
        /// <summary>防御側。</summary>
        public Sample_Unit Defender;
        /// <summary>ガード成立か（アクション層の判定結果）。</summary>
        public bool WasGuarded;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public int Value;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Defender = null;
            WasGuarded = false;
            Value = 0;
        }
    }
}
