// ============================================================================
// 【サンプルコード】Sample_PlayerController3D
// 入力→状態機械→ロジック呼び出し、を担うプレイヤー制御。
//
// ★パターン: 入力による状態変化（ステートマシン）
//   Idle（移動可）→ Attacking（前隙→有効フレーム→後隙）→ Idle
//   [G]長押しで Guarding（移動減速・攻撃不可）、HP0で Dead。
//
// ★パターン: ロジックとの役割分担
//   - 「攻撃を出せるか」= 発動判定セクション（スタミナ等。失敗レコードはロジックが出す）
//   - 「当たったか」    = Sample_HitboxTrigger3D（物理・有効フレーム・多段防止）
//   - 「当たった結果」  = HitResolutionSection（ここには一切のダメージ計算を書かない）
//
// 操作: WASD移動 / [1]斬り上げ / [3]溜め斬り / [4]鬼人薬 / [G]ガード
// ============================================================================

using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】状態機械つきプレイヤー制御（3D当たり判定版）。</summary>
    public sealed class Sample_PlayerController3D : MonoBehaviour
    {
        /// <summary>プレイヤーの状態。</summary>
        public enum State
        {
            /// <summary>待機・移動可能。</summary>
            Idle,
            /// <summary>攻撃中（前隙→有効→後隙）。</summary>
            Attacking,
            /// <summary>ガード姿勢（移動減速・攻撃不可）。</summary>
            Guarding,
            /// <summary>戦闘不能。</summary>
            Dead,
        }

        /// <summary>移動速度（m/s）。</summary>
        private const float MoveSpeed = 4f;

        /// <summary>ガード中の移動速度倍率。</summary>
        private const float GuardMoveMultiplier = 0.3f;

        /// <summary>攻撃の前隙（秒）。</summary>
        private const float WindupSeconds = 0.15f;

        /// <summary>攻撃判定の有効時間（秒）。</summary>
        private const float ActiveSeconds = 0.20f;

        /// <summary>攻撃の後隙（秒）。</summary>
        private const float RecoverySeconds = 0.25f;

        /// <summary>世界（ロジック側への窓口）。</summary>
        private Sample_ActionWorld _world;

        /// <summary>攻撃判定トリガー。</summary>
        private Sample_HitboxTrigger3D _hitbox;

        /// <summary>見た目（Animator反映＋簡易演出）。</summary>
        private Sample_SimpleCharacterView _view;

        /// <summary>現在の状態。</summary>
        public State Current { get; private set; } = State.Idle;

        /// <summary>ガード姿勢中か（敵AIが攻撃ヒット時に参照する）。</summary>
        public bool IsGuarding => Current == State.Guarding;

        /// <summary>攻撃状態の経過秒。</summary>
        private float _attackTime;

        /// <summary>実行中の攻撃モーション。</summary>
        private Sample_AttackMove _currentMove;

        /// <summary>依存を差し込む（生成直後に一度だけ呼ぶ）。</summary>
        public void Initialize(Sample_ActionWorld world, Sample_HitboxTrigger3D hitbox, Sample_SimpleCharacterView view)
        {
            _world = world;
            _hitbox = hitbox;
            _view = view;
            _hitbox.Initialize(OnHitboxHitPart);
            Current = State.Idle; // リスタート時に状態を戻す
            _attackTime = 0f;
        }

        /// <summary>毎フレーム、状態機械を進める。</summary>
        private void Update()
        {
            if (Current == State.Dead)
            {
                return;
            }
            if (_world.Hunter.IsDead)
            {
                TransitionToDead();
                return;
            }

            switch (Current)
            {
                case State.Idle:
                    UpdateIdle();
                    break;
                case State.Guarding:
                    UpdateGuarding();
                    break;
                case State.Attacking:
                    UpdateAttacking();
                    break;
            }
        }

        /// <summary>待機状態: 移動・ガード開始・攻撃開始を受け付ける。</summary>
        private void UpdateIdle()
        {
            Move(1f);

            if (Input.GetKey(KeyCode.G))
            {
                Current = State.Guarding;
                _view.SetGuarding(true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                TryStartAttack(_world.SlashUp);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TryStartAttack(_world.ChargedSlash);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                _world.Driver.UseDemonDrug(15, 20000);
            }
        }

        /// <summary>ガード状態: キーを離すと待機へ戻る。移動は減速。</summary>
        private void UpdateGuarding()
        {
            Move(GuardMoveMultiplier);

            if (!Input.GetKey(KeyCode.G))
            {
                Current = State.Idle;
                _view.SetGuarding(false);
            }
        }

        /// <summary>攻撃状態: 前隙→有効フレーム→後隙 を時間で進める。</summary>
        private void UpdateAttacking()
        {
            var before = _attackTime;
            _attackTime += Time.deltaTime;

            // 有効フレームの開始・終了エッジでヒットボックスを切り替える
            if (before < WindupSeconds && _attackTime >= WindupSeconds)
            {
                _hitbox.Activate();
            }
            var activeEnd = WindupSeconds + ActiveSeconds;
            if (before < activeEnd && _attackTime >= activeEnd)
            {
                _hitbox.Deactivate();
            }
            if (_attackTime >= activeEnd + RecoverySeconds)
            {
                Current = State.Idle;
            }
        }

        /// <summary>
        /// 攻撃を開始する。まず発動判定セクション（スタミナ等）に問い合わせ、
        /// 通らなければ状態は変えない（失敗レコードはロジック側が出す）。
        /// </summary>
        private void TryStartAttack(Sample_AttackMove move)
        {
            _world.Ctx.BeginResolution();
            if (!_world.Ctx.RunSection(Sample_ActivationCheckSection.Instance,
                    new Sample_ActivationInput(_world.Hunter, move)))
            {
                return;
            }

            _currentMove = move;
            _attackTime = 0f;
            Current = State.Attacking;
            _view.PlayAttack();
        }

        /// <summary>
        /// ヒットボックスが部位へ接触したときの処理。
        /// 「どの部位に・どのモーションで当たったか」だけを詰めてロジックへ解決を依頼する。
        /// </summary>
        private void OnHitboxHitPart(Sample_MonsterPart part)
        {
            _world.Ctx.BeginResolution();
            _world.Ctx.RunSection(Sample_HitResolutionSection.Instance,
                new Sample_HitRequest(_world.Hunter, _world.Monster, part, _currentMove, wasGuarded: false));
        }

        /// <summary>WASDで移動し、進行方向を向く。</summary>
        private void Move(float multiplier)
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude < 0.01f)
            {
                return;
            }
            var velocity = input.normalized * (MoveSpeed * multiplier);
            transform.position += velocity * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(input.normalized);
        }

        /// <summary>戦闘不能状態へ移行する。</summary>
        private void TransitionToDead()
        {
            Current = State.Dead;
            _view.SetGuarding(false);
            _view.PlayFaint();
        }
    }
}
