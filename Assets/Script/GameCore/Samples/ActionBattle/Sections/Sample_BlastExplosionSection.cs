namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】爆破ダメージセクション：肉質・会心を無視した固定ダメージ。
    /// 部位耐久減少セクションを再利用するため「爆破 → 部位破壊」の連鎖が自然に成立する。
    /// </summary>
    public sealed class Sample_BlastExplosionSection : Section<Sample_BlastInput, int>
    {
        /// <summary>爆破の固定ダメージ量（デモ用）。</summary>
        public const int BlastDamage = 120;

        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_BlastExplosionSection Instance = new Sample_BlastExplosionSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "爆破ダメージ";
        /// <summary>Sample_BlastExplosionSection を生成する。</summary>
        private Sample_BlastExplosionSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override int Execute(LogicContext ctx, in Sample_BlastInput input)
        {
            var wasDead = input.Target.IsDead;
            ((Sample_IUnitWriter)input.Target).ApplyDamage(BlastDamage);
            Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.StatusTriggered,
                targetId: Sample_ActionContext.Id(ctx, input.Target),
                partId: Sample_ActionContext.Id(ctx, input.Part),
                status: Sample_StatusKind.Blast,
                value: BlastDamage, value2: input.Target.Hp)); // HPはスナップショットで焼き込む

            // 戦闘不能は真実の持ち主が明示発行する（消費側にHP推論をさせない）
            if (!wasDead && input.Target.IsDead)
            {
                Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs,
                    Sample_RecordKind.Defeated,
                    targetId: Sample_ActionContext.Id(ctx, input.Target)));
            }

            if (input.Part != null)
            {
                // ★連鎖の連鎖: 爆破ダメージが部位破壊を引き起こしうる
                ctx.RunSection(Sample_PartDamageSection.Instance,
                    new Sample_PartDamageInput(input.Part, BlastDamage));
            }
            return BlastDamage;
        }
    }
}
