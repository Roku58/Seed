namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】スキル「攻撃力UP」: 攻撃力に乗算（不変の設定値のみ保持＝状態レス）。</summary>
    public sealed class Sample_AttackBoostHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_AttackPowerEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Hunter _self;
        /// <summary>乗算補正(‰)。</summary>
        private readonly int _permille;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Skill;

        /// <summary>Sample_AttackBoostHandler を生成する。</summary>
        public Sample_AttackBoostHandler(Sample_Hunter self, int permille) : base(self)
        {
            _self = self;
            _permille = permille;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_AttackPowerEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_AttackPowerEvent ev, LogicContext ctx)
        {
            if (ev.Attacker != _self)
            {
                return;
            }
            ev.Value = Permille.Apply(ev.Value, _permille);
        }
    }
}
