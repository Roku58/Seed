// ============================================================================
// 【サンプルコード】Sample_SimpleCharacterView
// レコード→キャラクター演出の反映先となる、最小のキャラクタービュー。
//
// ★パターン: Animatorへの反映
// Animator を Inspector で割り当てると SetTrigger / SetBool でそのまま反映される
// （パラメータ名: Attack / Hit / Faint / トリガー、Guarding / ブール）。
// Animator が無くても動くよう、Transform と色による簡易演出を内蔵しているため、
// プリミティブ（Capsule等）だけで見た目の確認ができる。
//
// 実プロジェクトでは、この「意味レベルのAPI（PlayAttack等）」を保ったまま
// 中身を本物のアニメーション・VFX 呼び出しに差し替えればよい。
// ============================================================================

using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】Animator反映＋Transformフォールバック演出つきの最小キャラビュー。</summary>
    public sealed class Sample_SimpleCharacterView : MonoBehaviour
    {
        /// <summary>簡易演出の種類。</summary>
        private enum Motion
        {
            /// <summary>何もしていない。</summary>
            None,
            /// <summary>前方への踏み込み（攻撃）。</summary>
            Attack,
            /// <summary>のけぞりと赤フラッシュ（被弾）。</summary>
            Hit,
            /// <summary>倒れる（戦闘不能）。</summary>
            Faint,
        }

        /// <summary>割り当てるとトリガー/ブールで反映される（無くても簡易演出で動く）。</summary>
        [SerializeField] private Animator _animator;

        /// <summary>攻撃トリガー名。</summary>
        private const string AttackTrigger = "Attack";

        /// <summary>被弾トリガー名。</summary>
        private const string HitTrigger = "Hit";

        /// <summary>戦闘不能トリガー名。</summary>
        private const string FaintTrigger = "Faint";

        /// <summary>ガード中ブール名。</summary>
        private const string GuardingBool = "Guarding";

        /// <summary>色替え対象のレンダラー。</summary>
        private Renderer _renderer;

        /// <summary>初期色（フラッシュから戻す用）。</summary>
        private Color _baseColor;

        /// <summary>初期ローカル座標（演出から戻す用）。</summary>
        private Vector3 _basePosition;

        /// <summary>初期回転（倒れ演出から戻す用）。</summary>
        private Quaternion _baseRotation;

        /// <summary>攻撃の踏み込み方向（正面）。</summary>
        private Vector3 _facing = Vector3.forward;

        /// <summary>再生中の簡易演出。</summary>
        private Motion _motion;

        /// <summary>簡易演出の経過秒。</summary>
        private float _motionTime;

        /// <summary>ガード中か（青みがかった表示にする）。</summary>
        private bool _isGuarding;

        /// <summary>テレグラフ（攻撃予兆）表示中か（黄色点滅）。</summary>
        private bool _isTelegraphing;

        /// <summary>初期状態を記憶する。</summary>
        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _baseColor = _renderer.material.color;
            }
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
        }

        /// <summary>簡易演出を毎フレーム進める。</summary>
        private void Update()
        {
            _motionTime += Time.deltaTime;
            ApplyMotion();
            ApplyTint();
        }

        /// <summary>正面方向（攻撃の踏み込み方向）を設定する。</summary>
        public void SetFacing(Vector3 facing)
        {
            _facing = facing.normalized;
        }

        /// <summary>攻撃演出を再生する（Animatorがあれば Attack トリガー）。</summary>
        public void PlayAttack()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(AttackTrigger);
            }
            StartMotion(Motion.Attack);
        }

        /// <summary>被弾演出を再生する（Animatorがあれば Hit トリガー）。</summary>
        public void PlayHit()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(HitTrigger);
            }
            StartMotion(Motion.Hit);
        }

        /// <summary>戦闘不能演出を再生する（Animatorがあれば Faint トリガー）。</summary>
        public void PlayFaint()
        {
            if (_animator != null)
            {
                _animator.SetTrigger(FaintTrigger);
            }
            StartMotion(Motion.Faint);
        }

        /// <summary>ガード状態の表示を切り替える（Animatorがあれば Guarding ブール）。</summary>
        public void SetGuarding(bool isGuarding)
        {
            if (_animator != null)
            {
                _animator.SetBool(GuardingBool, isGuarding);
            }
            _isGuarding = isGuarding;
        }

        /// <summary>攻撃予兆（テレグラフ）の表示を切り替える。</summary>
        public void SetTelegraphing(bool isTelegraphing)
        {
            _isTelegraphing = isTelegraphing;
        }

        /// <summary>位置・回転・色を初期状態へ戻す（再戦用）。</summary>
        public void ResetView()
        {
            _motion = Motion.None;
            _isGuarding = false;
            _isTelegraphing = false;
            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
            ApplyTint();
        }

        /// <summary>簡易演出を開始する。</summary>
        private void StartMotion(Motion motion)
        {
            // 倒れ演出は他の演出で上書きしない
            if (_motion == Motion.Faint)
            {
                return;
            }
            _motion = motion;
            _motionTime = 0f;
        }

        /// <summary>簡易演出（Transform操作）を適用する。</summary>
        private void ApplyMotion()
        {
            switch (_motion)
            {
                case Motion.Attack:
                {
                    // 0.3秒で 前へ0.6m → 戻る
                    var t = Mathf.Clamp01(_motionTime / 0.3f);
                    var swing = Mathf.Sin(t * Mathf.PI) * 0.6f;
                    transform.localPosition = _basePosition + _facing * swing;
                    if (t >= 1f)
                    {
                        _motion = Motion.None;
                    }
                    break;
                }
                case Motion.Hit:
                {
                    // 0.25秒の小刻みな揺れ
                    var t = Mathf.Clamp01(_motionTime / 0.25f);
                    var shake = Mathf.Sin(_motionTime * 60f) * 0.12f * (1f - t);
                    transform.localPosition = _basePosition + Vector3.right * shake;
                    if (t >= 1f)
                    {
                        _motion = Motion.None;
                    }
                    break;
                }
                case Motion.Faint:
                {
                    // 0.5秒かけて横に倒れる
                    var t = Mathf.Clamp01(_motionTime / 0.5f);
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, 90f * t);
                    break;
                }
            }
        }

        /// <summary>状態に応じた色（被弾=赤 / ガード=青 / 予兆=黄点滅）を適用する。</summary>
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
            else if (_isTelegraphing)
            {
                var pulse = Mathf.PingPong(Time.time * 4f, 1f);
                color = Color.Lerp(_baseColor, Color.yellow, pulse);
            }
            else if (_isGuarding)
            {
                color = Color.Lerp(_baseColor, Color.cyan, 0.6f);
            }
            _renderer.material.color = color;
        }
    }
}
