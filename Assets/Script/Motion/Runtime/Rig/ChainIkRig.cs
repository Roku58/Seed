using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// チェーンIKリグ（FABRIK の解を N 関節の Transform へ適用する）。
    /// 尻尾・触手・鎖・多関節ボスの腕など、2ボーンで表せない連結に使う。
    /// </summary>
    public sealed class ChainIkRig : IPoseRig
    {
        /// <summary>関節列（根本→先端）。</summary>
        private readonly Transform[] _joints;

        /// <summary>ソルバー用の位置バッファ（使い回し）。</summary>
        private readonly Vector3[] _positions;

        /// <summary>反復回数。</summary>
        private readonly int _iterations;

        /// <summary>目標（ワールド）。</summary>
        private Vector3 _target;

        /// <summary>目標が有効か。</summary>
        private bool _hasTarget;

        /// <summary>ChainIkRig を生成する（joints は根本→先端の順）。</summary>
        public ChainIkRig(Transform[] joints, int iterations = 8)
        {
            _joints = joints;
            _positions = new Vector3[joints.Length];
            _iterations = iterations;
        }

        /// <summary>適用率（0〜1）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>目標を更新する。</summary>
        public void SetTarget(Vector3 worldPosition)
        {
            _target = worldPosition;
            _hasTarget = true;
        }

        /// <summary>目標を解除する。</summary>
        public void ClearTarget()
        {
            _hasTarget = false;
        }

        /// <summary>FABRIK の解を回転差分で適用する。</summary>
        public void Apply()
        {
            if (!_hasTarget || Weight <= 0f || _joints.Length < 2)
            {
                return;
            }

            for (var i = 0; i < _joints.Length; i++)
            {
                _positions[i] = _joints[i].position;
            }
            FabrikSolver.Solve(_positions, _target, _iterations);

            // 根本から順に、子の現在方向→解の方向へ回す（回転で曲げる＝スキニング安全）
            for (var i = 0; i < _joints.Length - 1; i++)
            {
                var current = _joints[i + 1].position - _joints[i].position;
                var solved = _positions[i + 1] - _positions[i];
                var rotation = Quaternion.FromToRotation(current, solved) * _joints[i].rotation;
                _joints[i].rotation = Quaternion.Slerp(_joints[i].rotation, rotation, Weight);
            }
        }
    }
}
