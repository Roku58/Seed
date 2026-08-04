using System;
using Game.Battle.Contracts;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】出来事→リアクションの「方針」の置き場。
    ///
    /// 「被弾したらのけぞる」「死んだら倒れてリザルトを出す」は自明に見えて
    /// 実はゲーム演出方針（スーパーアーマー等の例外が将来ここに入る）。
    /// だからキャラ基盤に焼かず、Appの本クラスが通知→命令の変換として持つ。
    ///
    /// なおガード構え・攻撃の振りなど「自分で始める行動」の見た目は
    /// キャラ基盤の Behavior 遷移が直接駆動するため、ここでは扱わない
    /// （ここが扱うのは「外から降ってくる出来事」への反応だけ）。
    /// 命令の発行は PublishCommand（処理者1基盤の規約をランタイムが強制する）。
    /// </summary>
    public sealed class Sample_ReactionPolicy : IDisposable
    {
        /// <summary>保持中の購読。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(2);

        /// <summary>発行先のHub。</summary>
        private MessageHub _hub;

        /// <summary>Hubへ購読を張る。</summary>
        public void Initialize(MessageHub hub)
        {
            _hub = hub;
            hub.Subscribe<CharacterDamaged>(OnDamaged).AddTo(_subscriptions);
            hub.Subscribe<CharacterDied>(OnDied).AddTo(_subscriptions);
        }

        /// <summary>被弾 → のけぞり再生（将来: スーパーアーマー等の例外はここに書く）。</summary>
        private void OnDamaged(CharacterDamaged message)
        {
            _hub.PublishCommand(new PlayReactionCommand(message.Target, ReactionId.Hit));
        }

        /// <summary>死亡 → 倒れ再生＋リザルト画面へ（決着後はHUDへ戻らないので Replace）。</summary>
        private void OnDied(CharacterDied message)
        {
            _hub.PublishCommand(new PlayReactionCommand(message.Target, ReactionId.Death));
            _hub.PublishCommand(new ShowScreenCommand(Sample_ScreenIds.Result, ScreenTransition.Replace));
        }

        /// <summary>購読を片付ける。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
