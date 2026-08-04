using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 注視（LookAt）の姿勢解——頭・胸などを目標方向へ、可動域と重みつきで向ける。
    ///
    /// 純粋な回転計算（Transform非依存）:
    /// 1. 目標方向への完全な注視回転を作る
    /// 2. 中立姿勢からの角度が maxAngle を超えるぶんはクランプ（首がもげない）
    /// 3. 重みで中立姿勢とブレンド（0=アニメのまま、1=完全注視）
    /// 複数ボーンへ配分する場合は重みを分けて順に適用する（頭0.6+胸0.3 など）。
    /// </summary>
    public static class LookAtSolver
    {
        /// <summary>
        /// 注視回転を解く。
        /// neutralRotation … アニメが作った現在の回転（クランプの基準）
        /// bonePosition/targetPosition … ボーンと目標のワールド位置
        /// maxAngleDegrees … 中立からの最大回頭角
        /// weight … 適用率（0〜1）
        /// </summary>
        public static Quaternion Solve(Quaternion neutralRotation, Vector3 bonePosition,
            Vector3 targetPosition, float maxAngleDegrees, float weight)
        {
            var toTarget = targetPosition - bonePosition;
            if (toTarget.sqrMagnitude < 0.0001f || weight <= 0f)
            {
                return neutralRotation;
            }

            var full = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            var clamped = Quaternion.RotateTowards(neutralRotation, full, maxAngleDegrees);
            return Quaternion.Slerp(neutralRotation, clamped, Mathf.Clamp01(weight));
        }
    }
}
