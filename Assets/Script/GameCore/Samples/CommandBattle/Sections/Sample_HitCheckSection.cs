namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】命中判定セクション。命中率(‰)で抽選する。
    /// ※アクションバトルではコリジョンに置き換わり、このセクションは存在しない（取捨選択の例）。
    /// </summary>
    public sealed class Sample_HitCheckSection : Section<Sample_MoveInput, bool>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_HitCheckSection Instance = new Sample_HitCheckSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "命中判定";
        /// <summary>Sample_HitCheckSection を生成する。</summary>
        private Sample_HitCheckSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override bool Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            // 補足: 命中率がハンドラー補正で動的に 0/1000‰ を跨ぐ仕様にするなら
            //       Roll ではなく RollAlwaysConsume を使う（乱数消費数を一定に保つ）
            if (ctx.LogicRandom.Roll(input.Move.AccuracyPermille))
            {
                return true;
            }

            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.Missed,
                actor: input.User, target: input.Target, move: input.Move));
            return false;
        }
    }
}
