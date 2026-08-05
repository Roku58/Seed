using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 足IKリグ（階段・段差・坂の接地適応）。
    ///
    /// [何を解決するか] アニメーションクリップは平地を前提に作られているため、
    /// 階段や坂ではそのままだと足が地面から浮く/めり込む。本リグはアニメの上に
    /// 「実際の地面へ合わせる補正」を重ねる（LateUpdate の艶レイヤー）。
    ///
    /// [構成]
    /// - 地面の問い合わせ … <see cref="IGroundProbe"/>（Physics 実装を差し替え可能）
    /// - 接地の解き方 … <see cref="FootPlacementSolver"/>（純C#・段差上限・傾斜上限・時間追従）
    /// - 適用 … 脚は <see cref="TwoBoneIkRig"/> で曲げ、足首は法線へ沿わせる回転を掛ける
    ///
    /// [つま先レイ] Leg に Toe を渡すと、かかととつま先の2点で地面を探し**高い方**を採用する。
    /// 段差の縁に立ったとき、低い側に合わせて足が地面へ埋まるのを防ぐため。
    ///
    /// 真実（ActorPose・行動状態）には一切書き込まない。物理シーンからは読むだけ。
    /// </summary>
    public sealed class FootIkRig : IPoseRig
    {
        /// <summary>片足ぶんの構成。</summary>
        public readonly struct Leg
        {
            /// <summary>股ボーン（IKの根本）。</summary>
            public readonly Transform Hip;

            /// <summary>膝ボーン（IKの中間）。</summary>
            public readonly Transform Knee;

            /// <summary>足ボーン（IKの先端。ここを接地させる）。</summary>
            public readonly Transform Foot;

            /// <summary>つま先ボーン（任意。渡すと2点で地面を探して高い方を採る）。</summary>
            public readonly Transform Toe;

            /// <summary>Leg を生成する。</summary>
            public Leg(Transform hip, Transform knee, Transform foot, Transform toe = null)
            {
                Hip = hip;
                Knee = knee;
                Foot = foot;
                Toe = toe;
            }
        }

        /// <summary>腰（両足の高低差ぶん沈める対象）。</summary>
        private readonly Transform _hips;

        /// <summary>左足。</summary>
        private readonly Leg _left;

        /// <summary>右足。</summary>
        private readonly Leg _right;

        /// <summary>地面の問い合わせ先。</summary>
        private readonly IGroundProbe _probe;

        /// <summary>接地の解決器（調整値を内包）。</summary>
        private readonly FootPlacementSolver _solver;

        /// <summary>左足のIK。</summary>
        private readonly TwoBoneIkRig _leftIk;

        /// <summary>右足のIK。</summary>
        private readonly TwoBoneIkRig _rightIk;

        /// <summary>レイキャストの探索範囲（足の上下 ±この距離）。</summary>
        private readonly float _castRange;

        /// <summary>
        /// FootIkRig を生成する（地面問い合わせを注入する形。テスト・非物理地面にも対応）。
        /// </summary>
        public FootIkRig(Transform hips, Leg left, Leg right, IGroundProbe probe,
            FootIkSettings settings = null, float castRange = 1f)
        {
            _hips = hips;
            _left = left;
            _right = right;
            _probe = probe;
            _solver = new FootPlacementSolver(settings);
            _castRange = castRange;
            // 膝は前方へ曲がる（ポールヒントは適用時にキャラの前方から計算する）
            _leftIk = new TwoBoneIkRig(left.Hip, left.Knee, left.Foot, Vector3.zero);
            _rightIk = new TwoBoneIkRig(right.Hip, right.Knee, right.Foot, Vector3.zero);
        }

        /// <summary>
        /// FootIkRig を生成する（レイヤーマスク指定の簡易版。内部で
        /// <see cref="PhysicsGroundProbe"/> を作る）。
        /// </summary>
        public FootIkRig(Transform hips, Leg left, Leg right, LayerMask groundMask,
            FootIkSettings settings = null, float castRange = 1f)
            : this(hips, left, right, new PhysicsGroundProbe(groundMask), settings, castRange)
        {
        }

        /// <summary>適用率（0〜1。空中では呼び出し側が0にする、または自動フェードに任せる）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>調整値（実行中に書き換えてよい）。</summary>
        public FootIkSettings Settings => _solver.Settings;

        /// <summary>ワープ・リスポーン時に追従状態を捨てる（次フレームで即座に合わせる）。</summary>
        public void ResetFollow()
        {
            _solver.Reset();
        }

        /// <summary>両足の接地を解いて適用する（MotionRig が LateUpdate で呼ぶ）。</summary>
        public void Apply()
        {
            if (Weight <= 0f)
            {
                return; // 消されている間はレイキャストも省く
            }
            var up = _hips != null ? _hips.up : Vector3.up;
            var forward = _hips != null ? _hips.forward : Vector3.forward;

            // アニメだけを適用した足の状態を採り、その足下の地面を探す
            var leftSample = Sample(in _left, up);
            var rightSample = Sample(in _right, up);

            var solution = _solver.Solve(Time.deltaTime, in leftSample, in rightSample, up);

            // 腰を沈める（回転は触らない。位置の上下だけ）
            if (_hips != null && !Mathf.Approximately(solution.HipOffset, 0f))
            {
                _hips.position += up * (solution.HipOffset * Weight);
            }

            ApplyFoot(_leftIk, in _left, in solution.Left, forward);
            ApplyFoot(_rightIk, in _right, in solution.Right, forward);
        }

        /// <summary>片足ぶんの地面を探して <see cref="FootSample"/> を組む。</summary>
        private FootSample Sample(in Leg leg, Vector3 up)
        {
            var position = leg.Foot.position;
            var rotation = leg.Foot.rotation;
            var hasGround = TryProbeFrom(position, up, out var point, out var normal);

            // つま先も見て高い方を採る（段差の縁で足が埋まるのを防ぐ）
            if (leg.Toe != null
                && TryProbeFrom(leg.Toe.position, up, out var toePoint, out var toeNormal))
            {
                if (!hasGround || Vector3.Dot(toePoint - point, up) > 0f)
                {
                    // つま先側が高い: 高さはつま先に合わせ、法線は平均して急変を抑える
                    point = hasGround
                        ? point + up * Vector3.Dot(toePoint - point, up)
                        : toePoint;
                    normal = hasGround ? (normal + toeNormal).normalized : toeNormal;
                    hasGround = true;
                }
            }
            return new FootSample(position, rotation, hasGround, point, normal);
        }

        /// <summary>足位置の上方から真下へ地面を探す。</summary>
        private bool TryProbeFrom(Vector3 footPosition, Vector3 up,
            out Vector3 point, out Vector3 normal)
        {
            var origin = footPosition + up * _castRange;
            if (_probe != null
                && _probe.TryProbe(origin, -up, _castRange * 2f, out var hit))
            {
                point = hit.Point;
                normal = hit.Normal;
                return true;
            }
            point = default;
            normal = default;
            return false;
        }

        /// <summary>片足ぶんの解を適用する（脚のIK＋足首の回転）。</summary>
        private void ApplyFoot(TwoBoneIkRig ik, in Leg leg, in FootResult result, Vector3 forward)
        {
            var weight = result.Weight * Weight;
            if (weight <= 0f)
            {
                return;
            }
            ik.Weight = weight;
            ik.SetPoleHint(leg.Knee.position + forward); // 膝は前方へ曲げる
            ik.SetTarget(result.Position);
            ik.Apply();

            // 足首を法線へ沿わせる（IK で脚を曲げた後に上書きする＝順序が意味を持つ）
            leg.Foot.rotation = Quaternion.Slerp(leg.Foot.rotation, result.Rotation, weight);
        }
    }
}
