using System;

namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】技「まもる」ターン制版（★パターン: 発動判定に作用し、ターン終了で自己解除）。
    /// アクションバトル版の「ガード」はダメージ側に作用する別ハンドラー
    /// （Sample_GuardPerformanceHandler）。同じ概念でもバインドの差し替えだけで挙動を変えられる。
    /// 補足: 使用した瞬間から有効なのは、登録がイベント発火中ではなくセクション内で行われるため。
    /// </summary>
    public sealed class Sample_ProtectTurnHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_ActivationCheckEvent>,
        ILogicEventHandler<Sample_TurnEndEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Actor _self;

        /// <summary>Sample_ProtectTurnHandler を生成する。</summary>
        public Sample_ProtectTurnHandler(Sample_Actor self) : base(self)
        {
            _self = self;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_ActivationCheckEvent>(this);
            hub.Subscribe<Sample_TurnEndEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_ActivationCheckEvent ev, LogicContext ctx)
        {
            if (ev.Target != _self || ev.User == _self)
            {
                return;
            }
            ev.Failed = true;
            ev.Reason = Sample_FailReason.Blocked;
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_TurnEndEvent ev, LogicContext ctx)
        {
            // まもるは そのターン限り
            ctx.Hub.Unsubscribe(typeof(Sample_ActivationCheckEvent), this);
            ctx.Hub.Unsubscribe(typeof(Sample_TurnEndEvent), this);
        }
    }
}
