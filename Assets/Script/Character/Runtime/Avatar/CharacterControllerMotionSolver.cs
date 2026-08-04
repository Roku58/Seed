using System;
using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// CharacterController ベースの移動解決器（壁・段差・斜面に当てる本番用）。
    ///
    /// 純C#の Pose 駆動と物理の橋渡し:
    /// 1. Pose の現在位置へコントローラを合わせる（テレポート）
    /// 2. CharacterController.Move で希望変位を物理的に解決する
    /// 3. 実際の到達位置を返す → Behavior が Pose へ書き戻す
    /// これにより「真実の写しは常に ActorPose」という規約を保ったまま衝突が効く。
    /// </summary>
    public sealed class CharacterControllerMotionSolver : IMotionSolver
    {
        /// <summary>解決に使うコントローラ（Avatar の GameObject に付ける）。</summary>
        private readonly CharacterController _controller;

        /// <summary>CharacterControllerMotionSolver を生成する。</summary>
        public CharacterControllerMotionSolver(CharacterController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>希望変位を物理解決し、到達位置を返す。</summary>
        public Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta)
        {
            if (!_controller.gameObject.activeInHierarchy)
            {
                return currentPosition + desiredDelta; // 非表示Actor切替中などは素通し
            }
            // Pose とコントローラ位置のズレ（外部からの Pose 直接代入）を先に吸収する
            if ((_controller.transform.position - currentPosition).sqrMagnitude > 0.0001f)
            {
                _controller.transform.position = currentPosition;
            }
            _controller.Move(desiredDelta);
            return _controller.transform.position;
        }
    }
}
