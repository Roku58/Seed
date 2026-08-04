namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】とくせい「せいでんき」（★パターン: 割り込み＝ハンドラーから RunSection を呼び返す）。
    /// リアクションイベントに反応し、状態異常付与セクションを新たに呼び出す。ロジック側は無変更。
    /// </summary>
    public sealed class Sample_StaticAbilityHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_MoveReactionEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Actor _self;
        /// <summary>発動率(‰)。</summary>
        private readonly int _chancePermille;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Ability;

        /// <param name="chancePermille">発動率(‰)。本来300。デモでは1000（乱数を消費せず確定発動）。</param>
        public Sample_StaticAbilityHandler(Sample_Actor self, int chancePermille) : base(self)
        {
            _self = self;
            _chancePermille = chancePermille;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_MoveReactionEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_MoveReactionEvent ev, LogicContext ctx)
        {
            if (_self.IsFainted)
            {
                return;
            }
            if (ev.Defender != _self)
            {
                return;
            }
            if (!ev.Move.MakesContact)
            {
                return;
            }
            if (ev.DamageDealt <= 0)
            {
                return;
            }
            if (ev.Attacker.IsFainted)
            {
                return;
            }
            if (!ctx.LogicRandom.Roll(_chancePermille))
            {
                return;
            }

            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.AbilityTriggered,
                actor: _self, target: ev.Attacker, ability: Sample_AbilityKind.Static));
            ctx.RunSection(Sample_StatusInflictSection.Instance,
                new Sample_StatusInput(ev.Attacker, Sample_ConditionKind.Paralysis)); // ★割り込み
        }
    }
}
