namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】アイテム「鬼人薬」の効果（★パターン: 状態レスハンドラー）。
    /// 「効果中かどうか」「+いくつか」はアクター側の TimedCondition を毎回読む。
    /// ハンドラーは登録しっぱなしでよく、効果切れはコンディション失効だけで完結する。
    /// </summary>
    public sealed class Sample_DemonDrugHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_AttackPowerEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Hunter _self;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Item;

        /// <summary>Sample_DemonDrugHandler を生成する。</summary>
        public Sample_DemonDrugHandler(Sample_Hunter self) : base(self)
        {
            _self = self;
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
            if (!_self.Conditions.TryGet(Sample_ConditionKind.DemonDrug, ctx.NowMs, out var condition))
            {
                return;
            }
            ev.Value += condition.Magnitude;
        }
    }
}
