using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 移動（歩き・走りの総称）。
    /// 意図の方向へ Pose を進め、進行方向へ旋回する。
    /// 常に完了済み＝攻撃・被弾などから自由に割り込まれる。
    ///
    /// 位置の更新は <see cref="IMotionSolver"/> へ委譲する（既定は素通し）。
    /// CharacterController・NavMesh 等の実装に差し替えれば、本クラス無変更で
    /// 壁ずり・接地・経路制約が効く。
    /// </summary>
    public sealed class LocomotionBehavior : CharacterBehaviorBase
    {
        /// <summary>移動意図とみなす最小の二乗長。</summary>
        private const float MoveEpsilon = 0.0001f;

        /// <summary>移動速度（m/s）。</summary>
        private readonly float _moveSpeed;

        /// <summary>旋回速度（度/秒）。</summary>
        private readonly float _turnSpeedDegPerSec;

        /// <summary>LocomotionBehavior を生成する。</summary>
        public LocomotionBehavior(float moveSpeed, float turnSpeedDegPerSec = 720f)
        {
            _moveSpeed = moveSpeed;
            _turnSpeedDegPerSec = turnSpeedDegPerSec;
        }

        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Locomotion;

        /// <summary>意図の方向へ移動し、進行方向へ旋回する。</summary>
        public override void Tick(BehaviorContext context, float deltaTime)
        {
            var direction = context.Intent.MoveDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < MoveEpsilon)
            {
                context.Pose.PlanarSpeed = 0f;
                context.Avatar.SetLocomotionSpeed(0f);
                return;
            }

            // 意図の大きさ（0〜1）をそのまま速度スケールに使う（アナログ入力対応）
            var magnitude = Mathf.Min(direction.magnitude, 1f);
            var normalized = direction / direction.magnitude;
            var desiredDelta = normalized * (_moveSpeed * magnitude * deltaTime);
            context.Pose.Position = context.MotionSolver.Move(context.Pose.Position, desiredDelta);
            context.Pose.FaceTowards(normalized, _turnSpeedDegPerSec, deltaTime);
            context.Pose.PlanarSpeed = _moveSpeed * magnitude;
            context.Avatar.SetLocomotionSpeed(magnitude);
        }

        /// <summary>歩行表示を止める。</summary>
        public override void Exit(BehaviorContext context)
        {
            context.Pose.PlanarSpeed = 0f;
            context.Avatar.SetLocomotionSpeed(0f);
        }
    }
}
