using Seed.Input;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】このアプリ独自の入力アクションID（規約: ボタンの独自帯は 32〜63）。
    /// ホームのメニュー枠など、標準予約（1〜10）に無い意味はここで発番する。
    /// </summary>
    public static class Sample_ActionIds
    {
        /// <summary>メニュー4番（[4]キー。迷宮へ出撃）。</summary>
        public static readonly ActionId Slot4 = new ActionId(32);

        /// <summary>メニュー5番（[5]キー。市街へ出撃）。</summary>
        public static readonly ActionId Slot5 = new ActionId(33);

        /// <summary>[C] 視点切替（TPS→FPS→俯瞰の巡回。カメラ基盤への命令に使う）。</summary>
        public static readonly ActionId CycleView = new ActionId(34);
    }
}
