namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】発動判定イベント。Blocked にすると行動が失敗する。</summary>
    public sealed class Sample_ActivationCheckEvent : LogicEvent
    {
        /// <summary>行動主体。</summary>
        public Sample_Unit Actor;
        /// <summary>使用モーション/技。</summary>
        public Sample_AttackMove Move;
        /// <summary>true なら行動阻止。</summary>
        public bool Blocked;

        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
            Actor = null;
            Move = null;
            Blocked = false;
        }
    }
}
