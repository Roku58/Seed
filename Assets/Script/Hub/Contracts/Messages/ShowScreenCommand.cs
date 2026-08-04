namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 画面表示を依頼する命令（処理者はUI基盤のみ）。
    /// どの基盤からでも発行できるが、画面の実体はUI基盤しか知らない。
    /// </summary>
    public readonly struct ShowScreenCommand : ICommandMessage
    {
        /// <summary>表示したい画面。</summary>
        public readonly ScreenId Screen;

        /// <summary>履歴の積み方（既定は Push）。</summary>
        public readonly ScreenTransition Transition;

        /// <summary>画面へ渡す荷物（対象IDなど。意味はアプリ定義）。</summary>
        public readonly int Payload;

        /// <summary>ShowScreenCommand を生成する。</summary>
        public ShowScreenCommand(ScreenId screen,
            ScreenTransition transition = ScreenTransition.Push, int payload = 0)
        {
            Screen = screen;
            Transition = transition;
            Payload = payload;
        }
    }

    /// <summary>画面を1つ閉じる（最前面のモーダル/オーバーレイを畳む）命令。処理者はUI基盤のみ。</summary>
    public readonly struct CloseScreenCommand : ICommandMessage
    {
        /// <summary>閉じる対象のレイヤ。</summary>
        public readonly ScreenLayer Layer;

        /// <summary>CloseScreenCommand を生成する。</summary>
        public CloseScreenCommand(ScreenLayer layer = ScreenLayer.Modal)
        {
            Layer = layer;
        }
    }
}
