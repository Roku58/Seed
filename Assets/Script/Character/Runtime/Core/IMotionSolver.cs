using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 移動の解決器（物理・コリジョン・ナビメッシュとの統合点）。
    ///
    /// Behavior は「どれだけ動きたいか（希望変位）」だけを出し、
    /// 「実際にどこまで動けたか」はこの解決器が決める——
    /// 壁ずり・接地・経路制約の実装がここに差し替わっても、Behavior は一切変わらない。
    ///
    /// 実装の例:
    /// - DirectMotionSolver … 素通し（既定。障害物のない演出・2Dノベル等）
    /// - CharacterControllerMotionSolver … CharacterController で壁・段差に当てる
    /// - （アプリ実装）NavMeshベース … NavMesh.SamplePosition で経路上に丸める
    /// </summary>
    public interface IMotionSolver
    {
        /// <summary>希望変位を適用し、実際に到達した位置を返す。</summary>
        Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta);
    }

    /// <summary>素通しの解決器（既定。希望どおりに動く）。</summary>
    public sealed class DirectMotionSolver : IMotionSolver
    {
        /// <summary>共有インスタンス（状態を持たないため使い回せる）。</summary>
        public static readonly DirectMotionSolver Instance = new DirectMotionSolver();

        /// <summary>そのまま加算する。</summary>
        public Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta)
        {
            return currentPosition + desiredDelta;
        }
    }
}
