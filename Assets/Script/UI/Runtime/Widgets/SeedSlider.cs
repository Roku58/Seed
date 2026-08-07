using System;
using UnityEngine;
using UnityEngine.Events;

namespace Seed.UI
{
    /// <summary>
    /// スライダーの標準ラッパー（uGUI Slider 継承）。
    ///
    /// 足すものは2つ:
    /// - **購読の解除漏れ防止**: <see cref="OnValueChanged"/> が IDisposable を返す
    /// - **刻み（Step）**: 音量 0.05 刻み・難易度 1 刻みのような「飛び飛びの値」を
    ///   ドラッグ操作から作る。uGUI 標準の wholeNumbers は 1 刻み固定で、
    ///   0.05 のような任意刻みが書けないため
    ///
    /// コードから値を合わせるときは SetValueWithoutNotify を使う（トグルと同じ理由）。
    /// </summary>
    public class SeedSlider : UnityEngine.UI.Slider
    {
        /// <summary>値の刻み（0で自由値）。</summary>
        [SerializeField]
        private float _step;

        /// <summary>値の刻み（0で自由値）。</summary>
        public float Step
        {
            get => _step;
            set => _step = Mathf.Max(0f, value);
        }

        /// <summary>値の変化を購読する（戻り値の Dispose で解除）。</summary>
        public IDisposable OnValueChanged(Action<float> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            UnityAction<float> action = value => handler(value);
            onValueChanged.AddListener(action);
            return new ListenerToken(() => onValueChanged.RemoveListener(action));
        }

        /// <summary>値の反映（全経路がここを通るので、刻みへの吸着はこの1箇所で済む）。</summary>
        protected override void Set(float input, bool sendCallback = true)
        {
            if (_step > 0f)
            {
                input = Mathf.Round(input / _step) * _step;
            }
            base.Set(input, sendCallback);
        }
    }
}
