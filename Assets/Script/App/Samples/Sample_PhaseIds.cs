using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>【サンプル】このアプリのフェーズID一覧（PhaseIdの値割り当てはアプリの知識）。</summary>
    public static class Sample_PhaseIds
    {
        /// <summary>ホーム（出撃・買い物の起点）。</summary>
        public static readonly PhaseId Home = new PhaseId(1);

        /// <summary>戦闘（荷物 = StageId.Value）。</summary>
        public static readonly PhaseId Battle = new PhaseId(2);

        /// <summary>ショップ。</summary>
        public static readonly PhaseId Shop = new PhaseId(3);

        /// <summary>イベントADV（ショップ・会話イベント）。</summary>
        public static readonly PhaseId Event = new PhaseId(4);
    }
}
