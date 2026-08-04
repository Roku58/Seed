namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】武器固有「斬れ味」: 攻撃力に乗算。Weapon 優先度なのでスキルより先に掛かる。</summary>
    public sealed class Sample_SharpnessHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_AttackPowerEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Hunter _self;
        /// <summary>乗算補正(‰)。</summary>
        private readonly int _permille;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Weapon;

        /// <summary>Sample_SharpnessHandler を生成する。</summary>
        public Sample_SharpnessHandler(Sample_Hunter self, int permille) : base(self)
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
