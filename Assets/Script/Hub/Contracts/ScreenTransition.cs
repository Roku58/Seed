namespace Seed.Hub.Contracts
{
    /// <summary>画面の重ね方（レイヤ）。UI基盤はレイヤごとに現在の画面を管理する。</summary>
    public enum ScreenLayer
    {
        /// <summary>基底画面（同時に1枚。タイトル・インゲームHUD・リザルトなど）。</summary>
        Base = 0,

        /// <summary>基底の上に重ねる常設物（ミニマップ・通知バーなど）。</summary>
        Overlay = 1,

        /// <summary>入力を奪う前面（ポーズメニュー・確認ダイアログ・トースト）。</summary>
        Modal = 2,
    }

    /// <summary>
    /// 画面遷移のやり方。
    /// 履歴の積み方を発行側が選べないと「戻ってはいけない遷移」（タイトル→インゲーム）が
    /// 表現できないため、命令に乗せる。
    /// </summary>
    public enum ScreenTransition
    {
        /// <summary>履歴に積んで表示する（既定。Back で戻れる）。</summary>
        Push = 0,

        /// <summary>現在の画面を履歴に積まずに置き換える。</summary>
        Replace = 1,

        /// <summary>履歴を全消去して表示する（タイトルへ戻る等）。</summary>
        ClearAndShow = 2,
    }
}
