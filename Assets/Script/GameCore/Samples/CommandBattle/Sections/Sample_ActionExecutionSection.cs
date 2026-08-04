namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】行動実行セクション：1回の行動の入口。
    /// ※アクションバトルでは粒度が合わず使われない層（セクション取捨選択の例）。
    /// </summary>
    public sealed class Sample_ActionExecutionSection : Section<Sample_MoveInput, Sample_Nothing>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_ActionExecutionSection Instance = new Sample_ActionExecutionSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "行動実行";
        /// <summary>Sample_ActionExecutionSection を生成する。</summary>
        private Sample_ActionExecutionSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override Sample_Nothing Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.ActionDeclared,
                actor: input.User, move: input.Move));
            ctx.RunSection(Sample_MoveEffectSection.Instance, in input);
            return default;
        }
    }
}
