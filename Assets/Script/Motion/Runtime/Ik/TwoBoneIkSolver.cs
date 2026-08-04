using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 2ボーンIKの解析解（腕: 肩-肘-手 / 脚: 股-膝-足）。
    ///
    /// 純粋な位置計算（Transform非依存）なので EditMode で数学的に検証できる。
    /// 余弦定理で肘（中間関節）の曲げ角を決め、曲げ平面はポールヒントで制御する。
    /// 届かない目標へは一直線に伸ばして最も近い点を取る（クランプ）。
    /// Transform への適用は TwoBoneIkRig が回転差分（FromToRotation）で行う。
    /// </summary>
    public static class TwoBoneIkSolver
    {
        /// <summary>
        /// 関節位置を解く。a=根本（肩/股）は動かず、返り値は b=中間（肘/膝）と c=先端（手/足）の新位置。
        /// poleHint は曲げる方向の目印（肘なら背中側、膝なら前方の一点）。
        /// </summary>
        public static void Solve(Vector3 a, Vector3 b, Vector3 c, Vector3 target, Vector3 poleHint,
            out Vector3 newB, out Vector3 newC)
        {
            var lengthAb = Vector3.Distance(a, b);
            var lengthBc = Vector3.Distance(b, c);
            var maxReach = lengthAb + lengthBc;

            var toTarget = target - a;
            var distance = toTarget.magnitude;
            if (distance < 0.0001f)
            {
                // 目標が根本と同一点: 現状維持（解が定まらない）
                newB = b;
                newC = c;
                return;
            }
            var direction = toTarget / distance;

            // 届かない場合は一直線に伸ばしてクランプ
            if (distance >= maxReach)
            {
                newB = a + direction * lengthAb;
                newC = a + direction * maxReach;
                return;
            }

            // 余弦定理: 根本-目標 の線から肘をどれだけ持ち上げるか
            var clamped = Mathf.Max(distance, Mathf.Abs(lengthAb - lengthBc) + 0.0001f);
            var cosAngle = (lengthAb * lengthAb + clamped * clamped - lengthBc * lengthBc)
                / (2f * lengthAb * clamped);
            var angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f));

            // 曲げ平面の「持ち上げ方向」: 目標線と直交し、ポールヒント側を向く
            var poleDirection = Vector3.ProjectOnPlane(poleHint - a, direction);
            if (poleDirection.sqrMagnitude < 0.0001f)
            {
                // ポールが目標線上にある場合の保険（任意の直交軸）
                poleDirection = Vector3.Cross(direction, Vector3.up);
                if (poleDirection.sqrMagnitude < 0.0001f)
                {
                    poleDirection = Vector3.Cross(direction, Vector3.right);
                }
            }
            poleDirection.Normalize();

            newB = a + direction * (Mathf.Cos(angle) * lengthAb)
                + poleDirection * (Mathf.Sin(angle) * lengthAb);
            newC = target;
        }
    }
}
