namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】発動判定セクション（★パターン: SectionResult / EventScope）。
    /// スタミナ・拘束チェック。ハンター・モンスターの両方向で同じセクションを再利用する。
    /// </summary>
    public sealed class Sample_ActivationCheckSection : Section<Sample_ActivationInput, SectionResult>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_ActivationCheckSection Instance = new Sample_ActivationCheckSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "発動判定";
        /// <summary>Sample_ActivationCheckSection を生成する。</summary>
        private Sample_ActivationCheckSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override SectionResult Execute(LogicContext ctx, in Sample_ActivationInput input)
        {
            // 戦闘不能は行動不可（Fuzzテストのようなランダム入力に対する最終防衛線でもある）
            if (input.Actor.IsDead)
            {
                Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.ActionBlocked,
                    actorId: Sample_ActionContext.Id(ctx, input.Actor),
                    moveId: Sample_ActionContext.Id(ctx, input.Move),
                    block: Sample_BlockReason.Dead));
                /// <summary>失敗の結果を作る。</summary>
                return SectionResult.Fail((int)Sample_BlockReason.Dead);
            }

            if (input.Actor.Conditions.Has(Sample_ConditionKind.Paralyzed, ctx.NowMs))
            {
                Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.ActionBlocked,
                    actorId: Sample_ActionContext.Id(ctx, input.Actor),
                    moveId: Sample_ActionContext.Id(ctx, input.Move),
                    block: Sample_BlockReason.Paralyzed));
                /// <summary>失敗の結果を作る。</summary>
                return SectionResult.Fail((int)Sample_BlockReason.Paralyzed);
            }

            if (input.Actor is Sample_Hunter hunter && hunter.Stamina < input.Move.StaminaCost)
            {
                Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.ActionBlocked,
                    actorId: Sample_ActionContext.Id(ctx, input.Actor),
                    moveId: Sample_ActionContext.Id(ctx, input.Move),
                    block: Sample_BlockReason.Stamina,
                    value: hunter.Stamina, value2: input.Move.StaminaCost));
                /// <summary>失敗の結果を作る。</summary>
                return SectionResult.Fail((int)Sample_BlockReason.Stamina);
            }

            using (var scope = EventScope<Sample_ActivationCheckEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Actor = input.Actor;
                ev.Move = input.Move;
                ctx.Hub.Fire(ev, ctx);
                if (ev.Blocked)
                {
                    Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.ActionBlocked,
                        actorId: Sample_ActionContext.Id(ctx, input.Actor),
                        moveId: Sample_ActionContext.Id(ctx, input.Move),
                        block: Sample_BlockReason.Handler));
                    /// <summary>失敗の結果を作る。</summary>
                    return SectionResult.Fail((int)Sample_BlockReason.Handler);
                }
            }

            ((Sample_IUnitWriter)input.Actor).ConsumeStamina(input.Move.StaminaCost);
            /// <summary>成功の結果を作る。</summary>
            return SectionResult.Ok();
        }
    }
}
