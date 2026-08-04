namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】天気「晴れ」（★パターン: 1つの仕様が複数イベントに反応＝複数インターフェース実装）。
    /// </summary>
    public sealed class Sample_SunnyWeatherHandler : LogicEventHandlerBase,
        ILogicEventHandler<Sample_AttackPowerEvent>,
        ILogicEventHandler<Sample_DefensePowerEvent>
    {
        /// <summary>参照するフィールド状態。</summary>
        private readonly Sample_FieldState _field;

        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public override int Priority => Sample_HandlerOrder.Field;

        /// <summary>Sample_SunnyWeatherHandler を生成する。</summary>
        public Sample_SunnyWeatherHandler(Sample_FieldState field) : base(owner: null)
        {
            _field = field;
        }

        /// <summary>購読するイベントを登録する。</summary>
        public override void RegisterTo(EventHub hub)
        {
            hub.Subscribe<Sample_AttackPowerEvent>(this);
            hub.Subscribe<Sample_DefensePowerEvent>(this);
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_AttackPowerEvent ev, LogicContext ctx)
        {
            if (_field.Weather != Sample_Weather.Sunny)
            {
                return;
            }
            if (ev.Move.Element != Sample_Element.Fire)
            {
                return;
            }
            ev.Value = Permille.Apply(ev.Value, 1500); // ほのお技の攻撃力 x1.5
        }

        /// <summary>イベントに反応する処理。</summary>
        public void Handle(Sample_DefensePowerEvent ev, LogicContext ctx)
        {
            if (_field.Weather != Sample_Weather.Sunny)
            {
                return;
            }
            if (ev.Move.Element != Sample_Element.Water)
            {
                return;
            }
            ev.Value = Permille.Apply(ev.Value, 2000); // みず技被ダメ半減 = 防御2倍
        }
    }
}
