namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】防御力補正イベント（晴れの「みず技被ダメ半減」は防御2倍としてここに介入）。</summary>
    public sealed class Sample_DefensePowerEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Actor Attacker;
        /// <summary>防御側。</summary>
        public Sample_Actor Defender;
        /// <summary>使用モーション/技。</summary>
        public Sample_MoveData Move;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public int Value;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Defender = null;
            Move = null;
            Value = 0;
        }
    }
}
