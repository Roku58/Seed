namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】技データ（純データ）。
    /// 変化技の振る舞いは FactoryRegistry で技→ハンドラーを紐付ける（タイトルごとに差し替え可能）。
    /// </summary>
    public sealed class Sample_MoveData
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>技のタイプ。</summary>
        public Sample_Element Element { get; }
        /// <summary>技カテゴリ。</summary>
        public Sample_MoveCategory Category { get; }
        /// <summary>威力。</summary>
        public int Power { get; }
        /// <summary>命中率(‰)。1000なら乱数を消費せず必中（DeterministicRandom.Roll の規約）。</summary>
        public int AccuracyPermille { get; }
        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public int Priority { get; }
        /// <summary>接触技か。</summary>
        public bool MakesContact { get; }
        /// <summary>追加効果の状態異常。</summary>
        public Sample_ConditionKind SecondaryCondition { get; }
        /// <summary>追加効果の発生率(‰)。</summary>
        public int SecondaryChancePermille { get; }

        /// <summary>Sample_MoveData を生成する。</summary>
        public Sample_MoveData(string name, Sample_Element element, Sample_MoveCategory category, int power,
            int accuracyPermille, int priority = 0, bool makesContact = false,
            Sample_ConditionKind secondaryCondition = Sample_ConditionKind.None, int secondaryChancePermille = 0)
        {
            Name = name;
            Element = element;
            Category = category;
            Power = power;
            AccuracyPermille = accuracyPermille;
            Priority = priority;
            MakesContact = makesContact;
            SecondaryCondition = secondaryCondition;
            SecondaryChancePermille = secondaryChancePermille;
        }
    }
}
