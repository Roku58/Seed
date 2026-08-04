using System;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】スキル「ガード性能」: ガード成立時のダメージをチップ化（Lv2で完全無効）。
    /// 「ガードが成立したか」はアクション層の判定（HitRequest.WasGuarded）、
    /// 「成立したら何が起きるか」だけを本ハンドラーが担う——層の分離の実例。
    /// コマンドバトルの「まもる」（発動判定に作用）との対比も参照。
    /// </summary>
    public sealed class Sample_GuardPerformanceHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_FinalDamageEvent>
    {
        /// <summary>この仕様の持ち主。</summary>
        private readonly Sample_Hunter _self;
        /// <summary>ガード時に通す割合(‰)。</summary>
        private readonly int _chipPermille;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Skill;

        /// <summary>Sample_GuardPerformanceHandler を生成する。</summary>
        public Sample_GuardPerformanceHandler(Sample_Hunter self, int level) : base(self)
        {
            _self = self;
            _chipPermille = Math.Max(0, 300 - 150 * level);
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_FinalDamageEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_FinalDamageEvent ev, LogicContext ctx)
        {
            if (ev.Defender != _self || !ev.WasGuarded)
            {
                return;
            }
            ev.Value = Permille.Apply(ev.Value, _chipPermille);
        }
    }
}
