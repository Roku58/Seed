namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】モンスターの「怒り」（★パターン: 1仕様=複数イベント購読）。
    /// - HitResolvedEvent … 被ダメージを蓄積し、閾値到達で怒りコンディションを付与
    /// - FinalDamageEvent … 怒り中は被ダメージを軽減（Magnitude‰倍）
    /// 蓄積値・怒り状態はすべてアクター側にあり、ハンドラーは状態レス。
    /// </summary>
    public sealed class Sample_MonsterRageHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_HitResolvedEvent>,
        ILogicEventHandler<Sample_FinalDamageEvent>
    {
        /// <summary>怒りの持続時間（ミリ秒）。</summary>
        private const long RageDurationMs = 30000;
        /// <summary>怒り中の被ダメージ倍率(‰)。</summary>
        private const int RageDamageTakenPermille = 900;

        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Monster _self;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Monster;

        /// <summary>Sample_MonsterRageHandler を生成する。</summary>
        public Sample_MonsterRageHandler(Sample_Monster self) : base(self)
        {
            _self = self;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_HitResolvedEvent>(this);
            hub.Subscribe<Sample_FinalDamageEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_HitResolvedEvent ev, LogicContext ctx)
        {
            if (ev.Defender != _self)
            {
                return;
            }
            if (_self.Conditions.Has(Sample_ConditionKind.Enraged, ctx.NowMs))
            {
                return;
            }

            _self.RageAccumulated += ev.TotalDamage;
            if (_self.RageAccumulated < _self.RageThreshold)
            {
                return;
            }

            _self.RageAccumulated = 0;
            ((Sample_IUnitWriter)_self).AddConditionMerged(new TimedCondition<Sample_ConditionKind>(
                Sample_ConditionKind.Enraged, ctx.NowMs + RageDurationMs, RageDamageTakenPermille),
                ConditionMergePolicy.Overwrite, ctx.NowMs); // 再突入は上書き（★ポリシーの例）
            Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.ConditionAdded,
                actorId: Sample_ActionContext.Id(ctx, _self), condition: Sample_ConditionKind.Enraged,
                value: RageDamageTakenPermille, value2: (int)RageDurationMs));
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_FinalDamageEvent ev, LogicContext ctx)
        {
            if (ev.Defender != _self)
            {
                return;
            }
            if (!_self.Conditions.TryGet(Sample_ConditionKind.Enraged, ctx.NowMs, out var condition))
            {
                return;
            }
            ev.Value = Permille.Apply(ev.Value, condition.Magnitude);
        }
    }
}
