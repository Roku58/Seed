using System;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// キャラクター基盤とHubの接続点（購読の所有者・基盤の玄関）。
    /// PlayReactionCommand を受けて CharactersManager のリアクション采配へ渡し
    /// （Tick 中は保留・Tick 後に一括適用の2フェーズ規約）、
    /// 問い合わせ窓口（ICharacterQuery / ICharacterRoster）を ServiceRegistry へ貸し出す。
    /// UIもGameCoreも知らず、依存はHubと契約のみ。
    /// </summary>
    public sealed class CharacterSystem : IDisposable
    {
        /// <summary>保持中の購読（Disposeで一括解除）。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(2);

        /// <summary>全体管理（リアクション采配の委譲先）。</summary>
        private CharactersManager _characters;

        /// <summary>問い合わせ窓口の返却用。</summary>
        private ServiceRegistry _services;

        /// <summary>Hubへ購読を張り、問い合わせ窓口を貸し出す。</summary>
        public void Initialize(MessageHub hub, ServiceRegistry services, CharactersManager characters)
        {
            _characters = characters;
            _services = services;
            services.Register<ICharacterQuery>(characters.Registry);
            services.Register<ICharacterRoster>(characters.Registry);
            // 命令は SubscribeCommand（処理者1基盤の規約をランタイムが強制する）
            hub.SubscribeCommand<PlayReactionCommand>(OnPlayReaction).AddTo(_subscriptions);
        }

        /// <summary>リアクション命令を全体管理の采配へ渡す。</summary>
        private void OnPlayReaction(PlayReactionCommand command)
        {
            _characters.PostReaction(command.Target, command.Reaction, command.Payload);
        }

        /// <summary>購読と貸し出した窓口を片付ける。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
            _services?.Unregister<ICharacterQuery>();
            _services?.Unregister<ICharacterRoster>();
        }
    }
}
