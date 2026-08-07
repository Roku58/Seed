using System;
using UnityEngine;

namespace Seed.UI
{
    /// <summary>
    /// UI部品の共通フック（クリック音・触感などの一元化ポイント）。
    ///
    /// 各画面がボタンごとに SE 再生を書くと、音の変更・音量方針・ミュートが
    /// 画面の数だけ散らばる。部品側は「操作された」という事実だけをここへ流し、
    /// 鳴らすかどうか・何を鳴らすかは App が購読して決める（方針は App の規約）。
    /// </summary>
    public static class SeedUiEffects
    {
        /// <summary>UI部品が操作された（クリック・トグル切替・長押し成立）。</summary>
        public static event Action<Component> Interacted;

        /// <summary>操作を通知する（部品の内部から呼ぶ）。</summary>
        internal static void RaiseInteracted(Component source)
        {
            Interacted?.Invoke(source);
        }
    }

    /// <summary>
    /// リスナー解除のトークン（Dispose で購読が外れる）。
    ///
    /// UnityEvent の AddListener は解除を書き忘れやすく、破棄済み画面への配達で
    /// 例外や幽霊挙動を生む。Hub の購読と同じ「IDisposable を袋（SubscriptionBag）へ
    /// 入れて一括解除」の書き味に揃えるための小さな部品。
    /// </summary>
    internal sealed class ListenerToken : IDisposable
    {
        /// <summary>解除処理（一度呼んだら捨てる）。</summary>
        private Action _remove;

        /// <summary>ListenerToken を生成する。</summary>
        public ListenerToken(Action remove)
        {
            _remove = remove;
        }

        /// <summary>購読を解除する（二重 Dispose は無害）。</summary>
        public void Dispose()
        {
            _remove?.Invoke();
            _remove = null;
        }
    }
}
