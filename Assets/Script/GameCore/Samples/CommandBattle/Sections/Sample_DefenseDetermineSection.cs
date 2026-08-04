namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】防御力決定セクション。補正イベントを発火する。</summary>
    public sealed class Sample_DefenseDetermineSection : Section<Sample_MoveInput, int>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_DefenseDetermineSection Instance = new Sample_DefenseDetermineSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "防御力決定";
        /// <summary>Sample_DefenseDetermineSection を生成する。</summary>
        private Sample_DefenseDetermineSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override int Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            using (var scope = EventScope<Sample_DefensePowerEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.User;
                ev.Defender = input.Target;
                ev.Move = input.Move;
                ev.Value = input.Target.Defense;
                ctx.Hub.Fire(ev, ctx); // ← 晴れの「みず技被ダメ半減」は防御2倍としてここに介入
                return ev.Value;
            }
        }
    }
}
