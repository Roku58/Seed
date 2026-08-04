using System;
using System.Collections.Generic;

namespace Seed.Hub
{
    /// <summary>
    /// 基盤間で「同期の問い合わせ窓口」を貸し借りする台帳。
    ///
    /// メッセージ（出来事・命令）に対して、こちらは「今の状態を知りたい」用
    /// （例: ICharacterQuery.GetPosition）。毎フレームの連続値はメッセージに
    /// 流さずこちらで読む、が使い分けの規約。
    /// 登録できるのは Seed.Hub.Contracts のインターフェースのみ（実装型で引かない）。
    /// </summary>
    public sealed class ServiceRegistry
    {
        /// <summary>インターフェース型→実装の索引。</summary>
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>サービスを登録する。既定では二重登録を構成ミスとして例外にする。</summary>
        public void Register<TService>(TService service, bool allowOverwrite = false)
            where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            // 実装型で貸し借りすると基盤同士が実装を知ってしまうため、契約（IF）だけを許す。
            // 規約をコメントではなくコードで守らせる（開発ビルドのみ検査）。
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!typeof(TService).IsInterface)
            {
                throw new HubException(
                    $"サービスはインターフェースのみ登録可: {typeof(TService).Name}（実装型で引くと基盤が実装を知ってしまう）");
            }
#endif
            if (!allowOverwrite && _services.ContainsKey(typeof(TService)))
            {
                throw new HubException($"サービス {typeof(TService).Name} は登録済み（差し替えなら allowOverwrite: true）");
            }
            _services[typeof(TService)] = service;
        }

        /// <summary>サービスを取得する。未登録は合成ルートの配線漏れとして例外。</summary>
        public TService Resolve<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var service))
            {
                return (TService)service;
            }
            throw new HubException($"サービス {typeof(TService).Name} が未登録（合成ルートを確認）");
        }

        /// <summary>サービスの取得を試みる。</summary>
        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var value))
            {
                service = (TService)value;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>サービスを取り除く（シーン破棄時など）。</summary>
        public bool Unregister<TService>() where TService : class
        {
            return _services.Remove(typeof(TService));
        }
    }
}
