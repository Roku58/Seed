using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 注視リグ（頭・胸を目標へ向ける姿勢制御）。
    ///
    /// ボーン列と配分重みを渡す（例: 胸0.3 → 頭0.7 の順）。
    /// 目標は毎フレーム SetTarget で更新する（敵・会話相手・カメラ注視点など）。
    /// 重みを 0 にすればアニメそのまま——武器構え中は注視を切る、等は呼び出し側の方針。
    /// </summary>
    public sealed class LookAtRig : IPoseRig
    {
        /// <summary>ボーン1本の設定。</summary>
        private readonly struct Bone
        {
            /// <summary>対象ボーン。</summary>
            public readonly Transform Transform;

            /// <summary>このボーンへの配分重み。</summary>
            public readonly float Share;

            /// <summary>最大回頭角（度）。</summary>
            public readonly float MaxAngle;

            /// <summary>Bone を生成する。</summary>
            public Bone(Transform transform, float share, float maxAngle)
            {
                Transform = transform;
                Share = share;
                MaxAngle = maxAngle;
            }
        }

        /// <summary>ボーン列（根本→先端の順に適用）。</summary>
        private readonly Bone[] _bones;

        /// <summary>注視目標（ワールド）。</summary>
        private Vector3 _target;

        /// <summary>目標が有効か。</summary>
        private bool _hasTarget;

        /// <summary>LookAtRig を生成する（bones は (Transform, 配分, 最大角) の列）。</summary>
        public LookAtRig(params (Transform Bone, float Share, float MaxAngleDegrees)[] bones)
        {
            _bones = new Bone[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                _bones[i] = new Bone(bones[i].Bone, bones[i].Share, bones[i].MaxAngleDegrees);
            }
        }

        /// <summary>適用率（0〜1。距離フェード等は呼び出し側で）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>注視目標を更新する。</summary>
        public void SetTarget(Vector3 worldPosition)
        {
            _target = worldPosition;
            _hasTarget = true;
        }

        /// <summary>注視を解く（アニメが作った回転の上へ重ねる）。</summary>
        public void ClearTarget()
        {
            _hasTarget = false;
        }

        /// <summary>ボーン列へ注視回転を適用する。</summary>
        public void Apply()
        {
            if (!_hasTarget || Weight <= 0f)
            {
                return;
            }
            for (var i = 0; i < _bones.Length; i++)
            {
                var bone = _bones[i];
                if (bone.Transform == null)
                {
                    continue;
                }
                bone.Transform.rotation = LookAtSolver.Solve(
                    bone.Transform.rotation, bone.Transform.position, _target,
                    bone.MaxAngle, bone.Share * Weight);
            }
        }
    }
}
