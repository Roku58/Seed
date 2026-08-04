using System;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.UI
{
    /// <summary>
    /// UI基盤とHubの接続点（購読の所有者）。
    /// ShowScreenCommand / CloseScreenCommand を受けて Router を呼ぶ。
    /// 「命令の処理者は1基盤」の規約により、画面遷移の入口はここに一本化される
    /// （SubscribeCommand なのでランタイムが二重処理者を検知する）。
    ///
    /// Register を UISystem 経由にしているのは、画面へ Hub / ServiceRegistry の
    /// 足場（UIScreen.Attach）を配るため——画面がデータへ到達する正規ルートを
    /// 基盤が保証し、アプリの手書き配線を減らす。
    /// </summary>
    public sealed class UISystem : IDisposable
    {
        /// <summary>保持中の購読（Disposeで一括解除）。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(2);

        /// <summary>画面の交通整理役。</summary>
        private ScreenRouter _router;

        /// <summary>画面へ配る購読の足場。</summary>
        private MessageHub _hub;

        /// <summary>画面へ配る問い合わせの足場。</summary>
        private ServiceRegistry _services;

        /// <summary>Hubへ購読を張る。</summary>
        public void Initialize(MessageHub hub, ScreenRouter router, ServiceRegistry services)
        {
            _hub = hub;
            _router = router;
            _services = services;
            hub.SubscribeCommand<ShowScreenCommand>(OnShowScreen).AddTo(_subscriptions);
            hub.SubscribeCommand<CloseScreenCommand>(OnCloseScreen).AddTo(_subscriptions);
        }

        /// <summary>画面を登録し、Hub / Services の足場を配る（Register はこの経由が正規ルート）。</summary>
        public void Register(UIScreen screen)
        {
            screen.Attach(_hub, _services);
            _router.Register(screen);
        }

        /// <summary>画面表示命令をRouterへ流す。</summary>
        private void OnShowScreen(ShowScreenCommand command)
        {
            _router.Show(command.Screen, command.Transition, command.Payload);
        }

        /// <summary>画面を閉じる命令をRouterへ流す。</summary>
        private void OnCloseScreen(CloseScreenCommand command)
        {
            _router.Close(command.Layer);
        }

        /// <summary>
        /// 出入り演出を進める（合成ルートが毎フレーム呼ぶ。実時間dt推奨＝ポーズ中も画面は動ける）。
        /// 演出を使わない画面だけなら呼ばなくても動く（即時切替のまま）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _router.Tick(deltaTime);
        }

        /// <summary>購読を片付ける。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
