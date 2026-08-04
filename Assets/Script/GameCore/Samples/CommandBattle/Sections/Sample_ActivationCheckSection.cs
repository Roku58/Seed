namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】発動判定セクション（★パターン: SectionResult / EventScope）。
    /// 「ゲーム的に正常な失敗」を SectionResult で返し、失敗理由コードを載せる。
    /// </summary>
    public sealed class Sample_ActivationCheckSection : Section<Sample_MoveInput, SectionResult>
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
        protected override SectionResult Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            // 基盤ルール: ひんし状態は行動不可（進行役の確認漏れに対する最終防衛線）
            if (input.User.IsFainted)
            {
                Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                    Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.ActionFailed,
                    actor: input.User, move: input.Move, reason: Sample_FailReason.Fainted));
                /// <summary>失敗の結果を作る。</summary>
                return SectionResult.Fail((int)Sample_FailReason.Fainted);
            }

            // 基盤ルール: まひは 25% で行動不能
            if (input.User.Conditions.Has(Sample_ConditionKind.Paralysis, ctx.NowMs)
                && ctx.LogicRandom.Roll(250))
            {
                Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                    Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.ActionFailed,
                    actor: input.User, move: input.Move, reason: Sample_FailReason.Paralyzed));
                /// <summary>失敗の結果を作る。</summary>
                return SectionResult.Fail((int)Sample_FailReason.Paralyzed);
            }

            // ★パターン: EventScope による自動返却（Rent/Return の書き忘れが起きない）
            using (var scope = EventScope<Sample_ActivationCheckEvent>.Rent())
            {
                var ev = scope.Event;
                ev.User = input.User;
                ev.Target = input.Target;
                ev.Move = input.Move;
                ctx.Hub.Fire(ev, ctx); // ← ターン制の「まもる」はここで技を失敗させる

                if (ev.Failed)
                {
                    Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                        Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.ActionFailed,
                        actor: input.User, target: input.Target, move: input.Move, reason: ev.Reason));
                    /// <summary>失敗の結果を作る。</summary>
                    return SectionResult.Fail((int)ev.Reason);
                }
            }

            /// <summary>成功の結果を作る。</summary>
            return SectionResult.Ok();
        }
    }
}
