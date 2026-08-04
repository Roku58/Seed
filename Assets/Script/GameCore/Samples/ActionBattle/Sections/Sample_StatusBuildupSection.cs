namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】状態異常蓄積セクション：蓄積 → 閾値到達で発動し、効果セクションへ「連鎖」する。
    /// 発動ごとに耐性（閾値）が上がる。戻り値は連鎖で発生した追加ダメージ。
    /// </summary>
    public sealed class Sample_StatusBuildupSection : Section<Sample_StatusInput, int>
    {
        /// <summary>蓄積の初期閾値（デモ用）。</summary>
        public const int InitialTolerance = 30;
        /// <summary>まひ拘束時間（ミリ秒）。</summary>
        private const long ParalysisDurationMs = 8000;

        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_StatusBuildupSection Instance = new Sample_StatusBuildupSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "状態異常蓄積";
        /// <summary>Sample_StatusBuildupSection を生成する。</summary>
        private Sample_StatusBuildupSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override int Execute(LogicContext ctx, in Sample_StatusInput input)
        {
            int amount;
            using (var scope = EventScope<Sample_StatusBuildupEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Target = input.Target;
                ev.Status = input.Status;
                ev.Value = input.Amount;
                ctx.Hub.Fire(ev, ctx); // ← 状態異常強化スキル・モンスター耐性の介入ポイント
                amount = ev.Value;
            }

            var gauge = input.Target.GetGauge(input.Status, InitialTolerance);
            gauge.Accumulated += amount;
            Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.StatusBuildup,
                targetId: Sample_ActionContext.Id(ctx, input.Target), status: input.Status,
                value: gauge.Accumulated, value2: gauge.Tolerance));

            if (gauge.Accumulated < gauge.Tolerance)
            {
                return 0;
            }

            gauge.Accumulated = 0;
            gauge.Tolerance += gauge.ToleranceStep; // 発動ごとに耐性上昇

            switch (input.Status)
            {
                case Sample_StatusKind.Blast:
                    // ★連鎖: 蓄積の発動から新たなダメージ解決セクションを呼び出す
                    /// <summary>セクションを実行する。</summary>
                    return ctx.RunSection(Sample_BlastExplosionSection.Instance,
                        new Sample_BlastInput(input.Target, input.Part));

                case Sample_StatusKind.Paralysis:
                    ((Sample_IUnitWriter)input.Target).AddConditionMerged(
                        new TimedCondition<Sample_ConditionKind>(
                            Sample_ConditionKind.Paralyzed, ctx.NowMs + ParalysisDurationMs, 0),
                        ConditionMergePolicy.Extend, ctx.NowMs); // 再発動は拘束延長（★ポリシーの例）
                    Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.StatusTriggered,
                        targetId: Sample_ActionContext.Id(ctx, input.Target), status: input.Status,
                        value2: (int)ParalysisDurationMs));
                    return 0;

                default:
                    return 0;
            }
        }
    }
}
