namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】技効果後処理セクション。リアクションイベントを発火（「割り込み」の起点）。</summary>
    public sealed class Sample_PostMoveSection : Section<Sample_PostMoveInput, Sample_Nothing>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_PostMoveSection Instance = new Sample_PostMoveSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "技効果後処理";
        /// <summary>Sample_PostMoveSection を生成する。</summary>
        private Sample_PostMoveSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override Sample_Nothing Execute(LogicContext ctx, in Sample_PostMoveInput input)
        {
            using (var scope = EventScope<Sample_MoveReactionEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.User;
                ev.Defender = input.Target;
                ev.Move = input.Move;
                ev.DamageDealt = input.DamageDealt;
                ctx.Hub.Fire(ev, ctx);
            }
            return default;
        }
    }
}
