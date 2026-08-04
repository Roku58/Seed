using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 3Dモデルの Avatar（Animator反映＋プリミティブ用フォールバック演出）。
    ///
    /// Animator を割り当てるとトリガー/ブール/フロートで反映され、無くても
    /// Transformオフセットと色の簡易演出で動作確認ができる。
    /// 姿勢（ApplyPose）はルートの Transform へ、簡易演出のオフセットは子の
    /// _visualRoot へ適用し、移動と演出が干渉しない構造にしている
    /// （_visualRoot 未設定時はオフセット演出をスキップし、Animator と色のみ動く）。
    ///
    /// 規約「状態は Tick、艶は Update」——本クラスの Update が行うのは
    /// オフセット・色などの純装飾のみで、真実（位置・行動）には一切影響しない。
    /// </summary>
    public sealed class Avatar3D : MonoBehaviour, IAvatar
    {
        /// <summary>Animatorトリガー名: 攻撃。</summary>
        public const string AttackTrigger = "Attack";

        /// <summary>Animatorトリガー名: 被弾。</summary>
        public const string HitTrigger = "Hit";

        /// <summary>Animatorトリガー名: 戦闘不能。</summary>
        public const string DeathTrigger = "Faint";

        /// <summary>Animatorブール名: ガード中。</summary>
        public const string GuardingBool = "Guarding";

        /// <summary>Animatorフロート名: 移動速度（0〜1）。</summary>
        public const string SpeedFloat = "Speed";

        /// <summary>簡易演出の種類。</summary>
        private enum Motion
        {
            /// <summary>なし。</summary>
            None,
            /// <summary>前方への踏み込み。</summary>
            Attack,
            /// <summary>のけぞり＋赤フラッシュ。</summary>
            Hit,
            /// <summary>倒れる。</summary>
            Death,
        }

        /// <summary>割り当てるとAnimatorへ反映される（無くても簡易演出で動く）。</summary>
        [SerializeField] private Animator _animator;

        /// <summary>簡易演出のオフセット適用先（姿勢のルートと分けること）。</summary>
        [SerializeField] private Transform _visualRoot;

        /// <summary>色替え対象のレンダラー。</summary>
        private Renderer _renderer;

        /// <summary>初期色。</summary>
        private Color _baseColor;

        /// <summary>再生中の簡易演出。</summary>
        private Motion _motion;

        /// <summary>簡易演出の経過秒。</summary>
        private float _motionTime;

        /// <summary>現在の行動キー（色分け用）。</summary>
        private BehaviorKey _currentKey = BehaviorKey.None;

        /// <summary>アニメーションイベントの戻し先（Actor）。</summary>
        private IAvatarEventSink _eventSink;

        /// <summary>初期参照を確定する。</summary>
        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _baseColor = RendererTint.Get(_renderer);
            }
            // 自分自身をオフセット先にすると ApplyPose と喧嘩するため無効化する
            if (_visualRoot == transform)
            {
                _visualRoot = null;
            }
        }

        /// <summary>依存をコードから差し込む（合成ルートがプリミティブを組むとき用）。</summary>
        public void Configure(Animator animator, Transform visualRoot)
        {
            _animator = animator;
            _visualRoot = visualRoot == transform ? null : visualRoot;
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _baseColor = RendererTint.Get(_renderer);
            }
        }

        /// <summary>表示を立てる/降ろす。</summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>姿勢をルートの Transform へ反映する。</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>行動遷移を演出へ翻訳する。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
            _currentKey = next;

            // 倒れ姿勢からの復帰（Actor切替の force など）はオフセットを戻す
            if (previous.Equals(BehaviorKey.Death) && _visualRoot != null)
            {
                _visualRoot.localRotation = Quaternion.identity;
                _visualRoot.localPosition = Vector3.zero;
            }

            if (next.Equals(BehaviorKey.Attack))
            {
                if (_animator != null)
                {
                    _animator.SetTrigger(AttackTrigger);
                }
                StartMotion(Motion.Attack);
            }
            else if (next.Equals(BehaviorKey.Hit))
            {
                if (_animator != null)
                {
                    _animator.SetTrigger(HitTrigger);
                }
                StartMotion(Motion.Hit);
            }
            else if (next.Equals(BehaviorKey.Death))
            {
                if (_animator != null)
                {
                    _animator.SetTrigger(DeathTrigger);
                }
                _motion = Motion.Death;
                _motionTime = 0f;
            }

            if (_animator != null)
            {
                _animator.SetBool(GuardingBool, next.Equals(BehaviorKey.Guard));
            }
        }

        /// <summary>移動速度を歩行ブレンドへ反映する。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            if (_animator != null)
            {
                _animator.SetFloat(SpeedFloat, normalizedSpeed);
            }
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
        /// AnimationEvent の受信口。アニメクリップのイベントに本メソッドを
        /// int 引数（AvatarEventId の値）で仕込むと、現在の行動へ届く。
        /// 当たり判定フレーム・コンボ受付窓・モーション終了を「アニメ側の真実」で駆動できる。
        /// </summary>
        public void OnAnimationEvent(int eventId)
        {
            _eventSink?.PostAvatarEvent(eventId);
        }

        /// <summary>簡易演出（純装飾）を毎フレーム進める。</summary>
        private void Update()
        {
            _motionTime += Time.deltaTime;
            ApplyMotion();
            ApplyTint();
        }

        /// <summary>簡易演出を開始する（倒れ演出は上書きしない）。</summary>
        private void StartMotion(Motion motion)
        {
            if (_motion == Motion.Death)
            {
                return;
            }
            _motion = motion;
            _motionTime = 0f;
        }

        /// <summary>簡易演出（子オフセット操作）を適用する。</summary>
        private void ApplyMotion()
        {
            if (_visualRoot == null)
            {
                return;
            }

            switch (_motion)
            {
                case Motion.Attack:
                {
                    var t = Mathf.Clamp01(_motionTime / 0.3f);
                    var swing = Mathf.Sin(t * Mathf.PI) * 0.6f;
                    _visualRoot.localPosition = Vector3.forward * swing;
                    if (t >= 1f)
                    {
                        _visualRoot.localPosition = Vector3.zero;
                        _motion = Motion.None;
                    }
                    break;
                }
                case Motion.Hit:
                {
                    var t = Mathf.Clamp01(_motionTime / 0.25f);
                    var shake = Mathf.Sin(_motionTime * 60f) * 0.12f * (1f - t);
                    _visualRoot.localPosition = Vector3.right * shake;
                    if (t >= 1f)
                    {
                        _visualRoot.localPosition = Vector3.zero;
                        _motion = Motion.None;
                    }
                    break;
                }
                case Motion.Death:
                {
                    var t = Mathf.Clamp01(_motionTime / 0.5f);
                    _visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * t);
                    break;
                }
            }
        }

        /// <summary>状態に応じた色（被弾=赤 / ガード=シアン）を適用する。</summary>
        private void ApplyTint()
        {
            if (_renderer == null)
            {
                return;
            }
            var color = _baseColor;
            if (_motion == Motion.Hit)
            {
                color = Color.Lerp(Color.red, _baseColor, Mathf.Clamp01(_motionTime / 0.25f));
            }
            else if (_currentKey.Equals(BehaviorKey.Guard))
            {
                color = Color.Lerp(_baseColor, Color.cyan, 0.6f);
            }
            RendererTint.Set(_renderer, color);
        }
    }
}
