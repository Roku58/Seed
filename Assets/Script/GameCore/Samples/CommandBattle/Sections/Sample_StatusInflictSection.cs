namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】状態異常付与セクション（★パターン: AddOrMerge の Ignore ポリシー）。
    /// 技の追加効果からも、とくせいの「割り込み」からも再利用される汎用セクション。
    /// 発火するイベントが「連鎖」（クラボのみ）の起点になる。
    /// </summary>
    public sealed class Sample_StatusInflictSection : Section<Sample_StatusInput, bool>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_StatusInflictSection Instance = new Sample_StatusInflictSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "状態異常付与";
        /// <summary>Sample_StatusInflictSection を生成する。</summary>
        private Sample_StatusInflictSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override bool Execute(LogicContext ctx, in Sample_StatusInput input)
        {
            if (input.Target.IsFainted)
            {
                return false;
            }

            // ターン制では時間を進めないため持続は「永続」とし、治療で解除する。
            // 重ねがけは Ignore ポリシー＝すでに同じ状態なら何も起きない。
            var added = ((Sample_IActorWriter)input.Target).AddConditionMerged(
                new TimedCondition<Sample_ConditionKind>(input.Condition, long.MaxValue, 0),
                ConditionMergePolicy.Ignore, ctx.NowMs);
            if (!added)
            {
                return false;
            }

            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.StatusInflicted,
                target: input.Target, condition: input.Condition));

            using (var scope = EventScope<Sample_StatusInflictedEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Target = input.Target;
                ev.Condition = input.Condition;
                ctx.Hub.Fire(ev, ctx); // ← クラボのみ等がここに連鎖する
            }
            return true;
        }
    }
}
