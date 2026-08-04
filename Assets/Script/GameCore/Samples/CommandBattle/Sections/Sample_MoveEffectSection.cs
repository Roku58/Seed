namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】技効果セクション：発動判定 → 命中判定 → ダメージ付与 → 追加効果 → 技効果後処理。
    /// 変化技は FactoryRegistry（技→ハンドラー）で登録して終わる。
    /// </summary>
    public sealed class Sample_MoveEffectSection : Section<Sample_MoveInput, Sample_Nothing>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_MoveEffectSection Instance = new Sample_MoveEffectSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "技効果";
        /// <summary>Sample_MoveEffectSection を生成する。</summary>
        private Sample_MoveEffectSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override Sample_Nothing Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            if (!ctx.RunSection(Sample_ActivationCheckSection.Instance, in input))
            {
                return default;
            }

            // 変化技: タイトル側でバインドされたハンドラーを登録して終了（まもる等）
            if (input.Move.Category == Sample_MoveCategory.Status)
            {
                if (Sample_CommandContext.Bindings(ctx).TryCreate(input.Move, input.User, out var handler))
                {
                    handler.RegisterTo(ctx.Hub);
                    Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                        Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.Guarding,
                        actor: input.User, move: input.Move));
                }
                return default;
            }

            if (!ctx.RunSection(Sample_HitCheckSection.Instance, in input))
            {
                return default;
            }

            var damage = ctx.RunSection(Sample_DamageApplySection.Instance, in input);

            // 追加効果（例: ほのおのパンチ → 一定確率でやけど）
            if (input.Move.SecondaryCondition != Sample_ConditionKind.None
                && !input.Target.IsFainted && damage > 0
                && ctx.LogicRandom.Roll(input.Move.SecondaryChancePermille))
            {
                ctx.RunSection(Sample_StatusInflictSection.Instance,
                    new Sample_StatusInput(input.Target, input.Move.SecondaryCondition));
            }

            ctx.RunSection(Sample_PostMoveSection.Instance,
                new Sample_PostMoveInput(input.User, input.Target, input.Move, damage));
            return default;
        }
    }
}
