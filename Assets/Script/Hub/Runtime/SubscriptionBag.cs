using System;
using System.Collections.Generic;

namespace Seed.Hub
{
    /// <summary>
    /// 購読トークンを束ねて一括解除する袋。
    ///
    /// 全ての基盤（UI・キャラクター・翻訳者）が同じ `List&lt;IDisposable&gt;` を手書きしていたため、
    /// 「Add し忘れ / Dispose し忘れ」でリークした購読が破棄済みオブジェクトを呼ぶ事故が
    /// 構造的に起きていた。定型を基盤側に1つ持つことでそれを止める。
    ///
    /// Dispose 後の Add は即座に解除される（破棄後に購読が生き残らない）。
    /// </summary>
    public sealed class SubscriptionBag : IDisposable
    {
        /// <summary>保持中の購読トークン。</summary>
        private readonly List<IDisposable> _subscriptions;

        /// <summary>破棄済みか。</summary>
        private bool _isDisposed;

        /// <summary>SubscriptionBag を生成する。</summary>
        public SubscriptionBag(int capacity = 4)
        {
            _subscriptions = new List<IDisposable>(capacity);
        }

        /// <summary>保持件数（テスト・デバッグ用）。</summary>
        public int Count => _subscriptions.Count;

        /// <summary>破棄済みか。</summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>購読を預ける。破棄済みの袋に入れた購読は即座に解除される。</summary>
        public void Add(IDisposable subscription)
        {
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }
            if (_isDisposed)
            {
                subscription.Dispose();
                return;
            }
            _subscriptions.Add(subscription);
        }

        /// <summary>預かった購読をすべて解除する（二重 Dispose は無害）。</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }
            _subscriptions.Clear();
        }
    }

    /// <summary>購読を袋へ預ける書き味を提供する拡張。</summary>
    public static class SubscriptionBagExtensions
    {
        /// <summary>
        /// 購読トークンを袋へ預けてそのまま返す。
        /// <code>hub.Subscribe&lt;Foo&gt;(OnFoo).AddTo(_bag);</code> の形で、
        /// 購読と解除の宣言を1行に収めるための糖衣。
        /// </summary>
        public static IDisposable AddTo(this IDisposable subscription, SubscriptionBag bag)
        {
            if (bag == null)
            {
                throw new ArgumentNullException(nameof(bag));
            }
            bag.Add(subscription);
            return subscription;
        }
    }
}
