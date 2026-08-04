using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>【サンプル】このアプリの画面ID一覧（ScreenIdの値割り当てはアプリの知識）。</summary>
    public static class Sample_ScreenIds
    {
        /// <summary>バトル中のHUD画面。</summary>
        public static readonly ScreenId BattleHud = new ScreenId(1);

        /// <summary>決着後のリザルト画面。</summary>
        public static readonly ScreenId Result = new ScreenId(2);
    }
}
