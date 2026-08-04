// ============================================================================
// 【サンプルコード】Sample_MonsterController3D
// エネミーAIの実装例。
//
// ★パターン: 敵AIの状態機械
//   Cooldown（間合いを見ながら待つ）→ Telegraph（予兆: 黄色点滅で「来るぞ」を伝える）
//   → Strike（攻撃解決）→ Cooldown …の繰り返し。HP0で Dead。
//
// ★パターン: 命中の決め方はアクション層
//   射程内か（距離）・ガードされたか（プレイヤーの状態）をAIが判定し、
//   結果だけを HitRequest に詰めて HitResolutionSection へ渡す。
//   ダメージ計算・怒り移行などはすべてロジック側の仕事。
// ============================================================================

using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】予兆つき周期攻撃を行うモンスターAI。</summary>
    public sealed class Sample_MonsterController3D : MonoBehaviour
    {
        /// <summary>モンスターの状態。</summary>
        public enum State
        {
            /// <summary>次の攻撃までの待機（プレイヤーの方をゆっくり向く）。</summary>
            Cooldown,
            /// <summary>攻撃予兆（黄色点滅。ガードの猶予）。</summary>
            Telegraph,
            /// <summary>戦闘不能。</summary>
            Dead,
        }

        /// <summary>攻撃間隔（秒）。</summary>
        private const float CooldownSeconds = 3.0f;

        /// <summary>予兆の長さ（秒）。この間にガードを構えれば防げる。</summary>
        private const float TelegraphSeconds = 0.8f;

        /// <summary>尻尾回転の射程（m）。これより遠いと空振りになる。</summary>
        private const float StrikeRange = 3.5f;

        /// <summary>振り向き速度（度/秒）。</summary>
        private const float TurnSpeed = 90f;

        /// <summary>世界（ロジック側への窓口）。</summary>
        private Sample_ActionWorld _world;

        /// <summary>攻撃対象のプレイヤー。</summary>
        private Sample_PlayerController3D _player;

        /// <summary>見た目（予兆点滅・被弾演出）。</summary>
        private Sample_SimpleCharacterView _view;

        /// <summary>空振り時にログを出すための通知先。</summary>
        private System.Action<string> _pushLine;

        /// <summary>現在の状態。</summary>
        public State Current { get; private set; } = State.Cooldown;

        /// <summary>現在の状態の経過秒。</summary>
        private float _stateTime;

        /// <summary>依存を差し込む（生成直後に一度だけ呼ぶ）。</summary>
        public void Initialize(Sample_ActionWorld world, Sample_PlayerController3D player,
            Sample_SimpleCharacterView view, System.Action<string> pushLine)
        {
            _world = world;
            _player = player;
            _view = view;
            _pushLine = pushLine;
            Current = State.Cooldown; // リスタート時に状態を戻す
            _stateTime = 0f;
        }

        /// <summary>毎フレーム、AIの状態機械を進める。</summary>
        private void Update()
        {
            if (Current == State.Dead)
            {
                return;
            }
            if (_world.Monster.IsDead)
            {
                TransitionToDead();
                return;
            }

            _stateTime += Time.deltaTime;
            switch (Current)
            {
                case State.Cooldown:
                    UpdateCooldown();
                    break;
                case State.Telegraph:
                    UpdateTelegraph();
                    break;
            }
        }

        /// <summary>待機: プレイヤーの方へゆっくり向き、時間が来たら予兆へ。</summary>
        private void UpdateCooldown()
        {
            TurnTowardsPlayer();

            if (_stateTime >= CooldownSeconds)
            {
                Current = State.Telegraph;
                _stateTime = 0f;
                _view.SetTelegraphing(true);
            }
        }

        /// <summary>予兆: 点滅が終わった瞬間に攻撃を解決する。</summary>
        private void UpdateTelegraph()
        {
            if (_stateTime < TelegraphSeconds)
            {
                return;
            }
            _view.SetTelegraphing(false);
            _view.PlayAttack();
            Strike();
            Current = State.Cooldown;
            _stateTime = 0f;
        }

        /// <summary>
        /// 攻撃の解決。射程内か（距離）とガードされたか（プレイヤーの状態）を
        /// アクション層のここで決め、結果だけをロジックへ渡す。
        /// </summary>
        private void Strike()
        {
            var distance = Vector3.Distance(transform.position, _player.transform.position);
            if (distance > StrikeRange)
            {
                _pushLine?.Invoke($"{_world.Monster.Name}の尻尾回転は届かなかった（距離 {distance:0.0}m）");
                return;
            }

            var guarded = _player.IsGuarding;
            _world.Ctx.BeginResolution();
            _world.Ctx.RunSection(Sample_HitResolutionSection.Instance,
                new Sample_HitRequest(_world.Monster, _world.Hunter, null, _world.TailSwipe, guarded));
        }

        /// <summary>プレイヤーの方向へゆっくり旋回する。</summary>
        private void TurnTowardsPlayer()
        {
            var to = _player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
            {
                return;
            }
            var target = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * Time.deltaTime);
        }

        /// <summary>戦闘不能状態へ移行する。</summary>
        private void TransitionToDead()
        {
            Current = State.Dead;
            _view.SetTelegraphing(false);
            _view.PlayFaint();
        }
    }
}
