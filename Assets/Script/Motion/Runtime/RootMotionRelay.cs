using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// ルートモーションの中継（Animator の移動量を捕まえて、位置には直接適用しない）。
    ///
    /// [規約] 位置の真実は ActorPose——アニメが勝手にキャラを動かすと
    /// 「状態は Tick」の秩序が壊れる。そこで本クラスは OnAnimatorMove で
    /// 移動量を**貯めるだけ**にし、消費側（アプリの Tick）が TakeDelta() で取り出して
    /// 「希望変位」として MotionSolver / Pose へ流す——ルートモーション付きの
    /// 攻撃の踏み込みも、壁判定・リプレイと矛盾せずに効く。
    /// 使わない構成（インプレイスクリップ推奨）ではこのコンポーネント自体が不要。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class RootMotionRelay : MonoBehaviour
    {
        /// <summary>貯まっている移動量（ワールド）。</summary>
        private Vector3 _accumulatedDelta;

        /// <summary>貯まっている回転量。</summary>
        private Quaternion _accumulatedRotation = Quaternion.identity;

        /// <summary>Animator の適用を横取りして貯める（Transform には書かない）。</summary>
        private void OnAnimatorMove()
        {
            var animator = GetComponent<Animator>();
            _accumulatedDelta += animator.deltaPosition;
            _accumulatedRotation = animator.deltaRotation * _accumulatedRotation;
        }

        /// <summary>貯まった移動量を取り出してリセットする（毎Tick消費する）。</summary>
        public Vector3 TakeDelta()
        {
            var delta = _accumulatedDelta;
            _accumulatedDelta = Vector3.zero;
            return delta;
        }

        /// <summary>貯まった回転量を取り出してリセットする。</summary>
        public Quaternion TakeRotation()
        {
            var rotation = _accumulatedRotation;
            _accumulatedRotation = Quaternion.identity;
            return rotation;
        }
    }
}
