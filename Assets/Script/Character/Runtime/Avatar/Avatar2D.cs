using System;
using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 2D立ち絵（Sprite）の Avatar。
    /// 行動キー→スプライトの差し替え表で表情・ポーズを切り替え、
    /// 向き（yaw）は左右反転（flipX）で表現する。3D空間に置く場合はビルボードも可。
    ///
    /// 規約「状態は Tick、艶は Update」——Update が行うのは
    /// 被弾フラッシュの減衰・歩行バウンスなどの純装飾のみ。
    /// </summary>
    public sealed class Avatar2D : MonoBehaviour, IAvatar
    {
        /// <summary>行動キー→スプライトの対応1件。</summary>
        [Serializable]
        public struct SpriteEntry
        {
            /// <summary>行動キーの値（BehaviorKey.Value）。</summary>
            public int BehaviorKeyValue;

            /// <summary>表示するスプライト。</summary>
            public Sprite Sprite;
        }

        /// <summary>被弾フラッシュの長さ（秒）。</summary>
        private const float HitFlashSeconds = 0.25f;

        /// <summary>表示先のレンダラー。</summary>
        [SerializeField] private SpriteRenderer _renderer;

        /// <summary>行動→立ち絵の差し替え表（該当なしなら現状維持）。</summary>
        [SerializeField] private SpriteEntry[] _sprites;

        /// <summary>3D空間に置く場合、カメラへ正対させるか。</summary>
        [SerializeField] private bool _billboardToCamera;

        /// <summary>初期色。</summary>
        private Color _baseColor = Color.white;

        /// <summary>被弾フラッシュの残り秒。</summary>
        private float _hitFlashRemaining;

        /// <summary>歩行バウンスの振幅（0〜1）。</summary>
        private float _bounceWeight;

        /// <summary>バウンスの位相。</summary>
        private float _bouncePhase;

        /// <summary>現在の行動キー。</summary>
        private BehaviorKey _currentKey = BehaviorKey.None;

        /// <summary>アニメーションイベントの戻し先（Actor）。</summary>
        private IAvatarEventSink _eventSink;

        /// <summary>ルート位置（バウンスはローカルオフセットとしてレンダラーに乗せる）。</summary>
        private Vector3 _rendererBaseLocal;

        /// <summary>初期参照を確定する。</summary>
        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
                _rendererBaseLocal = _renderer.transform.localPosition;
            }
        }

        /// <summary>依存をコードから差し込む（合成ルートが動的に組むとき用）。</summary>
        public void Configure(SpriteRenderer renderer, SpriteEntry[] sprites, bool billboardToCamera = false)
        {
            _renderer = renderer;
            _sprites = sprites;
            _billboardToCamera = billboardToCamera;
            if (_renderer != null)
            {
                _baseColor = _renderer.color;
                _rendererBaseLocal = _renderer.transform.localPosition;
            }
        }

        /// <summary>表示を立てる/降ろす。</summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>位置を反映し、向きは左右反転で表現する。</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            if (_renderer != null)
            {
                var forward = rotation * Vector3.forward;
                // 右向き（x>0）を基準に、左向きのとき反転する（真正面はそのまま）
                if (Mathf.Abs(forward.x) > 0.05f)
                {
                    _renderer.flipX = forward.x < 0f;
                }
            }
        }

        /// <summary>行動遷移をスプライト差し替え＋演出へ翻訳する。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
            _currentKey = next;
            if (next.Equals(BehaviorKey.Hit))
            {
                _hitFlashRemaining = HitFlashSeconds;
            }
            ApplySprite(next);
        }

        /// <summary>移動速度を歩行バウンスの振幅へ反映する。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            _bounceWeight = Mathf.Clamp01(normalizedSpeed);
        }

        /// <summary>アニメーションイベントの戻し先を差し込む（Actor が自分を渡す）。</summary>
        public void BindEventSink(IAvatarEventSink sink)
        {
            _eventSink = sink;
        }

        /// <summary>表示物を解放する（GameObject ごと破棄）。</summary>
        public void Release()
        {
            _eventSink = null;
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// AnimationEvent の受信口（2DでもAnimationやTimelineから叩ける）。
        /// int 引数は AvatarEventId の値。現在の行動へ届く。
        /// </summary>
        public void OnAnimationEvent(int eventId)
        {
            _eventSink?.PostAvatarEvent(eventId);
        }

        /// <summary>純装飾（フラッシュ減衰・バウンス・ビルボード）を毎フレーム進める。</summary>
        private void Update()
        {
            if (_renderer == null)
            {
                return;
            }

            // 被弾フラッシュの減衰
            if (_hitFlashRemaining > 0f)
            {
                _hitFlashRemaining -= Time.deltaTime;
                var t = Mathf.Clamp01(_hitFlashRemaining / HitFlashSeconds);
                _renderer.color = Color.Lerp(_baseColor, Color.red, t);
            }
            else if (_currentKey.Equals(BehaviorKey.Guard))
            {
                _renderer.color = Color.Lerp(_baseColor, Color.cyan, 0.6f);
            }
            else
            {
                _renderer.color = _baseColor;
            }

            // 歩行バウンス（移動中だけ上下に弾む）
            if (_bounceWeight > 0.01f && !_currentKey.Equals(BehaviorKey.Death))
            {
                _bouncePhase += Time.deltaTime * 10f;
                var bounce = Mathf.Abs(Mathf.Sin(_bouncePhase)) * 0.1f * _bounceWeight;
                _renderer.transform.localPosition = _rendererBaseLocal + Vector3.up * bounce;
            }
            else
            {
                _bouncePhase = 0f;
                _renderer.transform.localPosition = _rendererBaseLocal;
            }

            // 倒れ表現（スプライト未登録でも伝わるよう横倒し）
            var fallen = _currentKey.Equals(BehaviorKey.Death);
            _renderer.transform.localRotation = fallen
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;

            // ビルボード
            if (_billboardToCamera && Camera.main != null)
            {
                var toCamera = _renderer.transform.position - Camera.main.transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.001f)
                {
                    _renderer.transform.rotation =
                        Quaternion.LookRotation(toCamera) * _renderer.transform.localRotation;
                }
            }
        }

        /// <summary>差し替え表からスプライトを適用する（該当なしなら現状維持）。</summary>
        private void ApplySprite(BehaviorKey key)
        {
            if (_renderer == null || _sprites == null)
            {
                return;
            }
            for (var i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i].BehaviorKeyValue == key.Value)
                {
                    _renderer.sprite = _sprites[i].Sprite;
                    return;
                }
            }
        }
    }
}
