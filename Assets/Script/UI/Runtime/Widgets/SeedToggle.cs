using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Seed.UI
{
    /// <summary>
    /// トグルの標準ラッパー（uGUI Toggle 継承）。
    ///
    /// 足すものは2つ:
    /// - **購読の解除漏れ防止**: <see cref="OnValueChanged"/> が IDisposable を返す
    ///   （SubscriptionBag へ AddTo できる）
    /// - **SE の一元化**: 操作（クリックでの切替）を <see cref="SeedUiEffects.Interacted"/> へ流す
    ///
    /// コードから値を合わせるときは uGUI 標準の SetIsOnWithoutNotify を使う——
    /// 「表示を状態へ合わせる」のと「ユーザーが操作した」を区別しないと、
    /// 設定画面を開いただけで設定変更の処理が走る事故になる。
    /// </summary>
    public class SeedToggle : UnityEngine.UI.Toggle
    {
        /// <summary>値の変化を購読する（戻り値の Dispose で解除）。</summary>
        public IDisposable OnValueChanged(Action<bool> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            UnityAction<bool> action = value => handler(value);
            onValueChanged.AddListener(action);
            return new ListenerToken(() => onValueChanged.RemoveListener(action));
        }

        /// <summary>クリック操作を共通フックへ流す（切替自体は基底に任せる）。</summary>
        public override void OnPointerClick(PointerEventData eventData)
        {
            var accepted = IsActive() && IsInteractable()
                && eventData.button == PointerEventData.InputButton.Left;
            base.OnPointerClick(eventData);
            if (accepted)
            {
                SeedUiEffects.RaiseInteracted(this);
            }
        }
    }
}
