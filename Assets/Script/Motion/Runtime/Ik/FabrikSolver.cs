using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// FABRIK（Forward And Backward Reaching IK）——N関節チェーンの反復IK。
    /// 尻尾・触手・鎖・多関節の腕など、2ボーンで表せないチェーンに使う。
    ///
    /// 前方パス（先端を目標へ置き根本へ向かって整列）と
    /// 後方パス（根本を固定し先端へ向かって整列）を反復する。
    /// 各ボーンの長さは常に保存される。純粋な位置計算＝EditModeで検証できる。
    /// </summary>
    public static class FabrikSolver
    {
        /// <summary>
        /// チェーンを解く（positions はワールド位置の列。先頭=根本は固定）。
        /// positions は書き換えられる。tolerance に届いたら早期終了する。
        /// </summary>
        public static void Solve(Vector3[] positions, Vector3 target,
            int iterations = 8, float tolerance = 0.001f)
        {
            if (positions == null || positions.Length < 2)
            {
                return;
            }

            var count = positions.Length;
            var lengths = new float[count - 1];
            var totalLength = 0f;
            for (var i = 0; i < count - 1; i++)
            {
                lengths[i] = Vector3.Distance(positions[i], positions[i + 1]);
                totalLength += lengths[i];
            }

            var root = positions[0];

            // 届かない目標: 一直線に伸ばして終わり（反復不要）
            if (Vector3.Distance(root, target) >= totalLength)
            {
                var direction = (target - root).normalized;
                for (var i = 1; i < count; i++)
                {
                    positions[i] = positions[i - 1] + direction * lengths[i - 1];
                }
                return;
            }

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                // 前方パス: 先端を目標へ置き、根本方向へ長さを保って整列
                positions[count - 1] = target;
                for (var i = count - 2; i >= 0; i--)
                {
                    var direction = (positions[i] - positions[i + 1]).normalized;
                    positions[i] = positions[i + 1] + direction * lengths[i];
                }

                // 後方パス: 根本を固定へ戻し、先端方向へ整列
                positions[0] = root;
                for (var i = 1; i < count; i++)
                {
                    var direction = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + direction * lengths[i - 1];
                }

                if (Vector3.Distance(positions[count - 1], target) <= tolerance)
                {
                    return; // 収束
                }
            }
        }
    }
}
