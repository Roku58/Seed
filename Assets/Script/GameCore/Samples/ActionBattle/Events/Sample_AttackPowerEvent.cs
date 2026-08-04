namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】攻撃力補正イベント（斬れ味→スキル→鬼人薬の順に補正される）。</summary>
    public sealed class Sample_AttackPowerEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Unit Attacker;
        /// <summary>使用モーション/技。</summary>
        public Sample_AttackMove Move;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public int Value;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Move = null;
            Value = 0;
        }
    }
}
