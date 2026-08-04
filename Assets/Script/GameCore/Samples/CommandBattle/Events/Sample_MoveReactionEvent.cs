namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】技効果後のリアクションイベント（せいでんき等の「割り込み」の起点）。</summary>
    public sealed class Sample_MoveReactionEvent : LogicEvent
    {
        /// <summary>攻撃側。</summary>
        public Sample_Actor Attacker;
        /// <summary>防御側。</summary>
        public Sample_Actor Defender;
        /// <summary>使用モーション/技。</summary>
        public Sample_MoveData Move;
        /// <summary>与えたダメージ。</summary>
        public int DamageDealt;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Attacker = null;
            Defender = null;
            Move = null;
            DamageDealt = 0;
        }
    }
}
