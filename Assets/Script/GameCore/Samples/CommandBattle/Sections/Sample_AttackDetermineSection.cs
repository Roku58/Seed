namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】攻撃力決定セクション。補正イベントを発火する（個別ルールは書かない）。</summary>
    public sealed class Sample_AttackDetermineSection : Section<Sample_MoveInput, int>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_AttackDetermineSection Instance = new Sample_AttackDetermineSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "攻撃力決定";
        /// <summary>Sample_AttackDetermineSection を生成する。</summary>
        private Sample_AttackDetermineSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override int Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            using (var scope = EventScope<Sample_AttackPowerEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.User;
                ev.Defender = input.Target;
                ev.Move = input.Move;
                ev.Value = input.User.Attack;
                ctx.Hub.Fire(ev, ctx); // ← 天気「晴れ」などが補正
                return ev.Value;
            }
        }
    }
}
