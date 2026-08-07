using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 足IKの調整値（階段・坂・段差での見え方をデータで決める）。
    ///
    /// 既定値は人型（身長1.7m前後・歩幅0.6m前後）を想定した無難な値。
    /// キャラの体格や地形の荒さに応じて調整する。
    /// </summary>
    public sealed class FootIkSettings
    {
        /// <summary>足首の高さ（接地点から足ボーンまでのオフセット。m）。</summary>
        public float FootHeight = 0.1f;

        /// <summary>
        /// 足を合わせる段差の上限（m）。これを超える高低差は「そこは足場ではない」と判断して
        /// IKを切る——階段を上るとき、1段先の蹴上げに足が吸い付いて脚が伸び切るのを防ぐ。
        /// </summary>
        public float MaxStepHeight = 0.45f;

        /// <summary>
        /// 足裏を沿わせる傾斜の上限（度）。これより急な面は壁とみなし、回転合わせをしない
        /// （位置合わせだけ行う）——壁に足の裏を貼り付ける不自然さを避けるため。
        /// </summary>
        public float MaxSlopeDegrees = 50f;

        /// <summary>腰を下げられる上限（m）。下げすぎて座り込むのを防ぐ。</summary>
        public float MaxHipDrop = 0.35f;

        /// <summary>足の追従速度（m/s）。小さいほどぬるく、大きいほど地形に忠実。</summary>
        public float FootFollowSpeed = 3.5f;

        /// <summary>腰の追従速度（m/s）。足より遅くすると重心の移動が滑らかに見える。</summary>
        public float HipFollowSpeed = 1.8f;

        /// <summary>足の回転追従速度（度/s）。</summary>
        public float FootRotationSpeed = 360f;

        /// <summary>接地重みのフェード速度（1/s）。空中へ出た瞬間の切り替えを滑らかにする。</summary>
        public float WeightFadeSpeed = 6f;

        /// <summary>法線に沿って足を回転させるか（false なら位置合わせのみ）。</summary>
        public bool RotateToNormal = true;
    }

    /// <summary>片足ぶんの入力（アニメ適用後の足の状態と、その足下の地面）。</summary>
    public readonly struct FootSample
    {
        /// <summary>アニメだけを適用した足の位置（腰を動かす前・ワールド）。</summary>
        public readonly Vector3 AnimatedPosition;

        /// <summary>アニメだけを適用した足の回転。</summary>
        public readonly Quaternion AnimatedRotation;

        /// <summary>地面が見つかったか。</summary>
        public readonly bool HasGround;

        /// <summary>接地点（HasGround が false なら未定義）。</summary>
        public readonly Vector3 GroundPoint;

        /// <summary>接地面の法線（HasGround が false なら未定義）。</summary>
        public readonly Vector3 GroundNormal;

        /// <summary>FootSample を生成する。</summary>
        public FootSample(Vector3 animatedPosition, Quaternion animatedRotation,
            bool hasGround, Vector3 groundPoint, Vector3 groundNormal)
        {
            AnimatedPosition = animatedPosition;
            AnimatedRotation = animatedRotation;
            HasGround = hasGround;
            GroundPoint = groundPoint;
            GroundNormal = groundNormal;
        }

        /// <summary>地面が無い（空中）サンプルを作る糖衣。</summary>
        public static FootSample Airborne(Vector3 position, Quaternion rotation)
        {
            return new FootSample(position, rotation, false, default, default);
        }
    }

    /// <summary>片足ぶんの解（目標位置・目標回転・適用率）。</summary>
    public readonly struct FootResult
    {
        /// <summary>足を置く目標位置（ワールド）。</summary>
        public readonly Vector3 Position;

        /// <summary>足の目標回転（ワールド）。</summary>
        public readonly Quaternion Rotation;

        /// <summary>適用率（0=アニメのまま・1=完全に地形へ合わせる）。</summary>
        public readonly float Weight;

        /// <summary>FootResult を生成する。</summary>
        public FootResult(Vector3 position, Quaternion rotation, float weight)
        {
            Position = position;
            Rotation = rotation;
            Weight = weight;
        }
    }

    /// <summary>両足＋腰の解。</summary>
    public readonly struct FootSolution
    {
        /// <summary>腰を動かす量（上方向の符号つき距離。負値＝下げる）。</summary>
        public readonly float HipOffset;

        /// <summary>左足の解。</summary>
        public readonly FootResult Left;

        /// <summary>右足の解。</summary>
        public readonly FootResult Right;

        /// <summary>FootSolution を生成する。</summary>
        public FootSolution(float hipOffset, FootResult left, FootResult right)
        {
            HipOffset = hipOffset;
            Left = left;
            Right = right;
        }
    }

    /// <summary>
    /// 足の接地解決（純C#・階段/坂/段差対応）。
    ///
    /// [解き方]
    /// 1. 各足について「アニメ位置から地面へ合わせるのに必要な上下量」を上方向へ射影して測る
    /// 2. その量が段差上限を超える足は足場ではないと判断し、適用率を 0 へ落とす
    ///    （階段の蹴上げや穴の縁で脚が伸び切る/めり込むのを防ぐ）
    /// 3. 両足のうち「より深く下げる必要がある側」に合わせて腰を沈める（上限つき）——
    ///    片足だけ届かず伸び切る不自然さを避ける、アクションゲームの定番の作り
    /// 4. 足裏は法線へ沿わせる。ただし傾斜上限を超える面（壁）では回転合わせをしない
    /// 5. すべての量は時間追従（速度制限）で平滑化する。段差をまたぐ瞬間に足が飛ばないため
    ///
    /// [なぜ純C#か] 接地の解き方は数学であり、Physics に依存しない。
    /// 地面は <see cref="IGroundProbe"/> 経由で外から与えられるので、
    /// EditMode で階段・傾斜・穴を偽の地面として与えて挙動を検証できる。
    /// 真実（ActorPose・行動状態）には一切書き込まない「艶」の一部である。
    /// </summary>
    public sealed class FootPlacementSolver
    {
        /// <summary>調整値。</summary>
        public readonly FootIkSettings Settings;

        /// <summary>現在の腰の移動量（平滑化済み）。</summary>
        private float _hipOffset;

        /// <summary>左足の現在の上下量（平滑化済み）。</summary>
        private float _leftLift;

        /// <summary>右足の現在の上下量（平滑化済み）。</summary>
        private float _rightLift;

        /// <summary>左足の現在の適用率。</summary>
        private float _leftWeight;

        /// <summary>右足の現在の適用率。</summary>
        private float _rightWeight;

        /// <summary>左足の現在の回転（平滑化済み）。</summary>
        private Quaternion _leftRotation = Quaternion.identity;

        /// <summary>右足の現在の回転（平滑化済み）。</summary>
        private Quaternion _rightRotation = Quaternion.identity;

        /// <summary>初回 Solve 済みか（初回は平滑化せず即座に合わせる）。</summary>
        private bool _initialized;

        /// <summary>FootPlacementSolver を生成する。</summary>
        public FootPlacementSolver(FootIkSettings settings = null)
        {
            Settings = settings ?? new FootIkSettings();
        }

        /// <summary>内部状態を捨てる（ワープ・リスポーン時に呼ぶ。次の Solve が即座に合わせる）。</summary>
        public void Reset()
        {
            _hipOffset = 0f;
            _leftLift = 0f;
            _rightLift = 0f;
            _leftWeight = 0f;
            _rightWeight = 0f;
            _initialized = false;
        }

        /// <summary>両足＋腰を解く（up はキャラの上方向。通常は Vector3.up）。</summary>
        public FootSolution Solve(float deltaSeconds, in FootSample left, in FootSample right, Vector3 up)
        {
            up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector3.up;

            // 1〜2. 各足の必要上下量と、段差上限による足場判定
            var leftValid = TryMeasureLift(in left, up, out var leftTargetLift);
            var rightValid = TryMeasureLift(in right, up, out var rightTargetLift);

            // 3. 腰は「より深く沈める必要がある側」に合わせる（下げ方向のみ・上限つき）
            var hipTarget = 0f;
            if (leftValid)
            {
                hipTarget = Mathf.Min(hipTarget, leftTargetLift);
            }
            if (rightValid)
            {
                hipTarget = Mathf.Min(hipTarget, rightTargetLift);
            }
            hipTarget = Mathf.Max(hipTarget, -Settings.MaxHipDrop);

            // 4. 足裏の目標回転（傾斜上限を超える面では回転を合わせない）
            var leftTargetRotation = ResolveRotation(in left, leftValid, up);
            var rightTargetRotation = ResolveRotation(in right, rightValid, up);

            // 5. 時間追従（初回は即座に合わせて、出現フレームのガクつきを避ける）
            if (!_initialized)
            {
                _initialized = true;
                _hipOffset = hipTarget;
                _leftLift = leftValid ? leftTargetLift : 0f;
                _rightLift = rightValid ? rightTargetLift : 0f;
                _leftWeight = leftValid ? 1f : 0f;
                _rightWeight = rightValid ? 1f : 0f;
                _leftRotation = leftTargetRotation;
                _rightRotation = rightTargetRotation;
            }
            else
            {
                _hipOffset = Mathf.MoveTowards(_hipOffset, hipTarget, Settings.HipFollowSpeed * deltaSeconds);
                _leftLift = Mathf.MoveTowards(_leftLift, leftValid ? leftTargetLift : 0f,
                    Settings.FootFollowSpeed * deltaSeconds);
                _rightLift = Mathf.MoveTowards(_rightLift, rightValid ? rightTargetLift : 0f,
                    Settings.FootFollowSpeed * deltaSeconds);
                _leftWeight = Mathf.MoveTowards(_leftWeight, leftValid ? 1f : 0f,
                    Settings.WeightFadeSpeed * deltaSeconds);
                _rightWeight = Mathf.MoveTowards(_rightWeight, rightValid ? 1f : 0f,
                    Settings.WeightFadeSpeed * deltaSeconds);
                _leftRotation = Quaternion.RotateTowards(_leftRotation, leftTargetRotation,
                    Settings.FootRotationSpeed * deltaSeconds);
                _rightRotation = Quaternion.RotateTowards(_rightRotation, rightTargetRotation,
                    Settings.FootRotationSpeed * deltaSeconds);
            }

            return new FootSolution(
                _hipOffset,
                new FootResult(left.AnimatedPosition + up * _leftLift, _leftRotation, _leftWeight),
                new FootResult(right.AnimatedPosition + up * _rightLift, _rightRotation, _rightWeight));
        }

        /// <summary>
        /// 足を地面へ合わせるのに必要な上下量を測る。
        /// 段差上限を超える（＝足場として妥当でない）場合は false。
        /// </summary>
        private bool TryMeasureLift(in FootSample sample, Vector3 up, out float lift)
        {
            if (!sample.HasGround)
            {
                lift = 0f;
                return false;
            }
            var desired = sample.GroundPoint + up * Settings.FootHeight;
            lift = Vector3.Dot(desired - sample.AnimatedPosition, up);
            if (Mathf.Abs(lift) > Settings.MaxStepHeight)
            {
                lift = 0f;
                return false; // 高すぎ/低すぎ＝ここは足を置く場所ではない
            }
            return true;
        }

        /// <summary>
        /// 足裏を法線へ沿わせた回転を返す。
        /// 傾斜が上限を超える面や、法線合わせが無効なときはアニメの回転をそのまま返す。
        /// </summary>
        private Quaternion ResolveRotation(in FootSample sample, bool valid, Vector3 up)
        {
            if (!valid || !Settings.RotateToNormal)
            {
                return sample.AnimatedRotation;
            }
            var normal = sample.GroundNormal;
            if (normal.sqrMagnitude < 1e-8f)
            {
                return sample.AnimatedRotation;
            }
            normal = normal.normalized;
            if (Vector3.Angle(up, normal) > Settings.MaxSlopeDegrees)
            {
                return sample.AnimatedRotation; // 壁とみなす（足裏を貼り付けない）
            }
            // アニメの回転を保ったまま、足の上方向だけを法線へ倒す最小回転
            var footUp = sample.AnimatedRotation * Vector3.up;
            return Quaternion.FromToRotation(footUp, normal) * sample.AnimatedRotation;
        }
    }
}
