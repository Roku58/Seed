using System;

namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】どうぐ「クラボのみ」（★パターン: 連鎖 / 消費＝購読解除）。
    /// 状態異常イベントに反応してまひを治す。使い切りは Unsubscribe を呼ぶだけ。
    /// </summary>
    public sealed class Sample_CheriBerryHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_StatusInflictedEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Actor _self;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Item;

        /// <summary>Sample_CheriBerryHandler を生成する。</summary>
        public Sample_CheriBerryHandler(Sample_Actor self) : base(self)
        {
            _self = self;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_StatusInflictedEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_StatusInflictedEvent ev, LogicContext ctx)
        {
            if (ev.Target != _self)
            {
                return;
            }
            if (ev.Condition != Sample_ConditionKind.Paralysis)
            {
                return;
            }

            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.ItemConsumed,
                actor: _self, item: Sample_ItemKind.CheriBerry));
            ((Sample_IActorWriter)_self).CureCondition(Sample_ConditionKind.Paralysis);
            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.StatusCured,
                target: _self, condition: Sample_ConditionKind.Paralysis));

            ctx.Hub.Unsubscribe(typeof(Sample_StatusInflictedEvent), this); // ★消費＝解除
        }
    }
}
