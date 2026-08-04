using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 2ボーンIKリグ（腕・脚の Transform へソルバーの解を適用する）。
    ///
    /// 手を対象へ伸ばす・武器を掴む・足を接地させる、の実行部。
    /// 解は TwoBoneIkSolver（純数学）が出し、ここは回転差分への変換だけを行う——
    /// 位置を直接書かず回転で曲げるので、スキニングされたモデルでも破綻しない。
    /// </summary>
    public sealed class TwoBoneIkRig : IPoseRig
    {
        /// <summary>根本ボーン（肩/股）。</summary>
        private readonly Transform _root;

        /// <summary>中間ボーン（肘/膝）。</summary>
        private readonly Transform _mid;

        /// <summary>先端ボーン（手/足）。</summary>
        private readonly Transform _tip;

        /// <summary>曲げ方向の目印（ワールド。肘なら背中側の一点）。</summary>
        private Vector3 _poleHint;

        /// <summary>目標（ワールド）。</summary>
        private Vector3 _target;

        /// <summary>目標が有効か。</summary>
        private bool _hasTarget;

        /// <summary>TwoBoneIkRig を生成する。</summary>
        public TwoBoneIkRig(Transform root, Transform mid, Transform tip, Vector3 poleHint)
        {
            _root = root;
            _mid = mid;
            _tip = tip;
            _poleHint = poleHint;
        }

        /// <summary>適用率（0〜1。届く範囲の外では呼び出し側でフェードすると自然）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>目標を更新する。</summary>
        public void SetTarget(Vector3 worldPosition)
        {
            _target = worldPosition;
            _hasTarget = true;
        }

        /// <summary>目標を解除する（アニメの姿勢へ戻る）。</summary>
        public void ClearTarget()
        {
            _hasTarget = false;
        }

        /// <summary>曲げ方向の目印を更新する。</summary>
        public void SetPoleHint(Vector3 worldPosition)
        {
            _poleHint = worldPosition;
        }

        /// <summary>解を回転差分としてボーンへ適用する。</summary>
        public void Apply()
        {
            if (!_hasTarget || Weight <= 0f || _root == null || _mid == null || _tip == null)
            {
                return;
            }

            var a = _root.position;
            var b = _mid.position;
            var c = _tip.position;
            TwoBoneIkSolver.Solve(a, b, c, _target, _poleHint, out var newB, out var newC);

            // 根本: 現在の中間方向 → 新しい中間方向 へ回す
            var rootRotation = Quaternion.FromToRotation(b - a, newB - a) * _root.rotation;
            _root.rotation = Quaternion.Slerp(_root.rotation, rootRotation, Weight);

            // 中間: （根本の回転で動いた後の）現在の先端方向 → 目標方向 へ回す
            var midRotation = Quaternion.FromToRotation(_tip.position - _mid.position,
                newC - _mid.position) * _mid.rotation;
            _mid.rotation = Quaternion.Slerp(_mid.rotation, midRotation, Weight);
        }
    }
}
