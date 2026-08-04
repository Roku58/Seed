namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】部位耐久減少セクション。破壊で肉質が軟化し、部位破壊レコードを出す。</summary>
    public sealed class Sample_PartDamageSection : Section<Sample_PartDamageInput, bool>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_PartDamageSection Instance = new Sample_PartDamageSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "部位耐久減少";
        /// <summary>Sample_PartDamageSection を生成する。</summary>
        private Sample_PartDamageSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override bool Execute(LogicContext ctx, in Sample_PartDamageInput input)
        {
            int amount;
            using (var scope = EventScope<Sample_PartDamageEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Part = input.Part;
                ev.Value = input.Amount;
                ctx.Hub.Fire(ev, ctx); // ← 破壊王などの介入ポイント
                amount = ev.Value;
            }

            if (!input.Part.AccumulateDamage(amount))
            {
                return false;
            }

            Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs, Sample_RecordKind.PartBroken,
                partId: Sample_ActionContext.Id(ctx, input.Part),
                value: input.Part.HitZonePercent));
            return true;
        }
    }
}
