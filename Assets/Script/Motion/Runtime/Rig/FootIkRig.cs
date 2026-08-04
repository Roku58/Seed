using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 足IKリグ（両足の接地適応。坂・階段・段差でアニメの足が浮く/めり込むのを直す）。
    ///
    /// 各足の上方からレイキャストで地面を探し、TwoBoneIK で足を着地点へ合わせる。
    /// 両足の高低差ぶん腰を沈める（低い方の足に合わせる）ことで、
    /// 「片足だけ届かず伸び切る」不自然さを避ける——アクションゲームの定番の作り。
    /// レイキャストは物理シーンへの読み取りだけで、真実（ActorPose）には書かない。
    /// </summary>
    public sealed class FootIkRig : IPoseRig
    {
        /// <summary>片足ぶんの構成。</summary>
        public readonly struct Leg
        {
            /// <summary>股ボーン。</summary>
            public readonly Transform Hip;

            /// <summary>膝ボーン。</summary>
            public readonly Transform Knee;

            /// <summary>足ボーン。</summary>
            public readonly Transform Foot;

            /// <summary>Leg を生成する。</summary>
            public Leg(Transform hip, Transform knee, Transform foot)
            {
                Hip = hip;
                Knee = knee;
                Foot = foot;
            }
        }

        /// <summary>腰（両足の高低差ぶん沈める対象）。</summary>
        private readonly Transform _hips;

        /// <summary>左足。</summary>
        private readonly Leg _left;

        /// <summary>右足。</summary>
        private readonly Leg _right;

        /// <summary>地面とみなすレイヤー。</summary>
        private readonly LayerMask _groundMask;

        /// <summary>足首の高さ（接地点から足ボーンまでのオフセット）。</summary>
        private readonly float _footHeight;

        /// <summary>レイキャストの探索範囲（足の上下 ±この距離）。</summary>
        private readonly float _castRange;

        /// <summary>左足のIK。</summary>
        private readonly TwoBoneIkRig _leftIk;

        /// <summary>右足のIK。</summary>
        private readonly TwoBoneIkRig _rightIk;

        /// <summary>FootIkRig を生成する。</summary>
        public FootIkRig(Transform hips, Leg left, Leg right, LayerMask groundMask,
            float footHeight = 0.1f, float castRange = 1f)
        {
            _hips = hips;
            _left = left;
            _right = right;
            _groundMask = groundMask;
            _footHeight = footHeight;
            _castRange = castRange;
            // 膝は前方へ曲がる（ポールヒントは適用時に前方から計算する）
            _leftIk = new TwoBoneIkRig(left.Hip, left.Knee, left.Foot, Vector3.zero);
            _rightIk = new TwoBoneIkRig(right.Hip, right.Knee, right.Foot, Vector3.zero);
        }

        /// <summary>適用率（0〜1。空中では呼び出し側が0にする）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>両足の接地を解いて適用する。</summary>
        public void Apply()
        {
            if (Weight <= 0f)
            {
                return; // 空中などで消されている間はレイキャストも省く
            }
            var leftHit = TryFindGround(_left.Foot.position, out var leftPoint);
            var rightHit = TryFindGround(_right.Foot.position, out var rightPoint);
            if (!leftHit && !rightHit)
            {
                return; // 空中・地面レイヤー外
            }

            // 腰を低い方の足へ寄せる（高低差の吸収。回転は触らない）
            if (leftHit && rightHit && _hips != null)
            {
                var drop = Mathf.Min(
                    leftPoint.y - _left.Foot.position.y,
                    rightPoint.y - _right.Foot.position.y);
                if (drop < 0f)
                {
                    _hips.position += new Vector3(0f, drop * Weight, 0f);
                }
            }

            // 各足を接地点へ（膝の曲げ方向はキャラの前方）
            var forward = _hips != null ? _hips.forward : Vector3.forward;
            if (leftHit)
            {
                _leftIk.Weight = Weight;
                _leftIk.SetPoleHint(_left.Knee.position + forward);
                _leftIk.SetTarget(leftPoint + Vector3.up * _footHeight);
                _leftIk.Apply();
            }
            if (rightHit)
            {
                _rightIk.Weight = Weight;
                _rightIk.SetPoleHint(_right.Knee.position + forward);
                _rightIk.SetTarget(rightPoint + Vector3.up * _footHeight);
                _rightIk.Apply();
            }
        }

        /// <summary>足位置の上方から地面を探す。</summary>
        private bool TryFindGround(Vector3 footPosition, out Vector3 groundPoint)
        {
            var origin = footPosition + Vector3.up * _castRange;
            if (Physics.Raycast(origin, Vector3.down, out var hit, _castRange * 2f, _groundMask))
            {
                groundPoint = hit.point;
                return true;
            }
            groundPoint = default;
            return false;
        }
    }
}
