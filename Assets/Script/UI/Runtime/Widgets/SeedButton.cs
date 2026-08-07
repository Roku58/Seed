using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Seed.UI
{
    /// <summary>
    /// ボタンの標準ラッパー（uGUI Button 継承。コードから制御する前提）。
    ///
    /// [素の Button に足りないもの＝このラッパーが足すもの]
    /// - **連打防止**: クールダウン中のクリックを無視する（購入・遷移の二重実行事故を型で防ぐ）
    /// - **処理中ロック**: <see cref="BeginBusy"/> の間は押せない（非同期処理の再入防止。
    ///   using で書けるので解除漏れがない）
    /// - **購読の解除漏れ防止**: <see cref="OnClick"/> が IDisposable を返す
    ///   （Hub の購読と同じ書き味。SubscriptionBag へ AddTo できる）
    /// - **長押し**: <see cref="OnLongPress"/>（成立した押下ではクリックを発火しない）
    /// - **押下の沈み込み**: スケールの艶（コード制御。アニメーターアセット不要）
    /// - **SE の一元化**: 操作を <see cref="SeedUiEffects.Interacted"/> へ流す
    ///
    /// uGUI の Button を継承しているので、既存のプレハブ・raycast・遷移設定はそのまま使える。
    /// インスペクターの OnClick() 登録も生きる（コード購読と同じ経路で発火する）。
    /// </summary>
    public class SeedButton : UnityEngine.UI.Button
    {
        /// <summary>連打防止のクールダウン（秒）。0で無効。</summary>
        [SerializeField]
        private float _cooldownSeconds = 0.15f;

        /// <summary>長押しの成立時間（秒）。0で長押し無効。</summary>
        [SerializeField]
        private float _longPressSeconds;

        /// <summary>押下中の沈み込みスケール（1で無効）。</summary>
        [SerializeField]
        private float _pressScale = 0.94f;

        /// <summary>長押しの判定（純C#・テスト済み）。</summary>
        private readonly LongPressTracker _longPress = new LongPressTracker();

        /// <summary>長押しの購読者。</summary>
        private Action _longPressed;

        /// <summary>次にクリックを受け付ける時刻。</summary>
        private float _nextAcceptTime;

        /// <summary>処理中ロックの重なり数（非同期が並んでも最後の解除まで押せない）。</summary>
        private int _busyCount;

        /// <summary>ロック前の interactable（解除時に元へ戻す）。</summary>
        private bool _interactableBeforeBusy;

        /// <summary>沈み込みの基準スケール。</summary>
        private Vector3 _baseScale = Vector3.one;

        /// <summary>基準スケールを記録済みか（艶で潰れた値を基準にしないため）。</summary>
        private bool _baseScaleCaptured;

        /// <summary>
        /// 時刻の供給源（既定は実時間）。実時間依存の判定をテストで
        /// 待ち時間なしに検証するための差し替え口——ポーズ中もUIは押せるべきなので
        /// unscaled を使う。
        /// </summary>
        public Func<float> TimeSource { get; set; } = () => Time.unscaledTime;

        /// <summary>連打防止のクールダウン（秒）。</summary>
        public float CooldownSeconds
        {
            get => _cooldownSeconds;
            set => _cooldownSeconds = Mathf.Max(0f, value);
        }

        /// <summary>長押しの成立時間（秒）。0で無効。</summary>
        public float LongPressSeconds
        {
            get => _longPressSeconds;
            set => _longPressSeconds = Mathf.Max(0f, value);
        }

        /// <summary>処理中ロック中か。</summary>
        public bool IsBusy => _busyCount > 0;

        /// <summary>クリックを購読する（戻り値の Dispose で解除。インスペクター登録と同じ経路）。</summary>
        public IDisposable OnClick(Action handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            UnityAction action = () => handler();
            onClick.AddListener(action);
            return new ListenerToken(() => onClick.RemoveListener(action));
        }

        /// <summary>長押しを購読する（LongPressSeconds が 0 なら発火しない）。</summary>
        public IDisposable OnLongPress(Action handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            _longPressed += handler;
            return new ListenerToken(() => _longPressed -= handler);
        }

        /// <summary>
        /// 処理中ロックを開始する（Dispose で解除）。
        /// セーブ・通信・画面遷移など「終わるまで押させたくない」区間を using で囲む。
        /// </summary>
        public IDisposable BeginBusy()
        {
            if (_busyCount == 0)
            {
                _interactableBeforeBusy = interactable;
                interactable = false; // 見た目も無効化（押せない理由が伝わる）
            }
            _busyCount++;
            return new ListenerToken(EndBusy);
        }

        /// <summary>処理中ロックを1つ解除する。</summary>
        private void EndBusy()
        {
            _busyCount--;
            if (_busyCount <= 0)
            {
                _busyCount = 0;
                interactable = _interactableBeforeBusy;
            }
        }

        /// <summary>クリック（ポインタ）。クールダウン・処理中・長押し成立後は無視する。</summary>
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            if (_longPress.Fired)
            {
                _longPress.Reset(); // 長押しとして消費済みの押下はクリックにしない
                return;
            }
            TryAccept();
        }

        /// <summary>クリック（決定キー・ゲームパッド）。</summary>
        public override void OnSubmit(BaseEventData eventData)
        {
            TryAccept();
        }

        /// <summary>押下開始: 長押しの計時を始める。</summary>
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (IsActive() && IsInteractable())
            {
                _longPress.Begin(TimeSource());
            }
        }

        /// <summary>押下終了: 計時を止める（成立済みフラグはクリック抑止のため残す）。</summary>
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _longPress.Cancel();
        }

        /// <summary>受付判定と発火（すべてのクリック経路はここを通る）。</summary>
        private void TryAccept()
        {
            if (!IsActive() || !IsInteractable() || _busyCount > 0)
            {
                return;
            }
            var now = TimeSource();
            if (_cooldownSeconds > 0f && now < _nextAcceptTime)
            {
                return; // 連打・二重クリックの吸収
            }
            _nextAcceptTime = now + _cooldownSeconds;
            SeedUiEffects.RaiseInteracted(this);
            onClick.Invoke();
        }

        /// <summary>基準スケールを最初の有効化時に記録する。</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            if (!_baseScaleCaptured)
            {
                _baseScale = transform.localScale;
                _baseScaleCaptured = true;
            }
        }

        /// <summary>艶: 押下の沈み込みと長押しの成立監視（実時間で動く＝ポーズ中も反応）。</summary>
        private void Update()
        {
            if (_longPress.TryFire(TimeSource(), _longPressSeconds))
            {
                SeedUiEffects.RaiseInteracted(this);
                _longPressed?.Invoke();
            }

            if (_pressScale < 1f)
            {
                var target = IsPressed() ? _baseScale * _pressScale : _baseScale;
                transform.localScale = Vector3.Lerp(transform.localScale, target,
                    1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            }
        }
    }
}
