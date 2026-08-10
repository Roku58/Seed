using Seed.Character;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】キネマティック移動モーター（CapsuleCollider＋Rigidbody。IMotionSolver）。
    ///
    /// Unity 標準の CharacterController は使わない（プロジェクト方針）。実体は
    /// **CapsuleCollider ＋ kinematic Rigidbody** で、移動の解決は自前の
    /// collide-and-slide（カプセルキャストで衝突を探し、残り変位を面に沿わせる）が行う:
    /// - 壁ずり: 衝突面へ残りを射影して滑る（最大3回反復＝曲がり角も1Tickで曲がれる）
    /// - 段差: 壁状の面に当たったら段の高さを測って持ち上げる（前進は残り変位のまま）
    /// - 坂: 登坂可能な法線（SlopeLimit 以内）なら射影のまま沿って登る
    /// - 重力・ジャンプ: 垂直速度を内部で積分（接地中は軽い押し付けで吸着）
    /// - 接地: 足元の球オーバーラップ＋離地猶予（ちらつき→滞空ポーズ連打の防止）
    ///
    /// [なぜ kinematic か] 位置の真実は ActorPose——Tick が同期的に確定する設計のため、
    /// 物理ステップ待ちで位置が非同期に変わる動的 Rigidbody は使えない。kinematic なら
    /// Move の戻り値＝そのTickの確定位置のまま、コライダーが「他者から見える物理的な体」
    /// （トリガー検知・キャストの対象・押し合いの相手）として立つ。
    ///
    /// [自己ヒット対策] 全クエリは自分の Rigidbody に付いたコライダーを除外する
    /// （レイヤーに頼らない＝キャラ同士が同じレイヤーでも互いを遮れる）。
    /// </summary>
    public sealed class Sample_KinematicMotor : IMotionSolver
    {
        /// <summary>面から保つ隙間（m。キャスト誤差・貫通を防ぐ緩衝）。</summary>
        private const float SkinWidth = 0.03f;

        /// <summary>壁ずりの最大反復。</summary>
        private const int MaxSlideIterations = 3;

        /// <summary>乗り越えられる段差の高さ（m。蹴上げ0.18mの階段を確実に）。</summary>
        private const float StepOffset = 0.4f;

        /// <summary>登坂限界の法線Y（cos50°。これより急な面は壁として扱う）。</summary>
        private const float SlopeLimitY = 0.64f;

        /// <summary>離地をなかったことにする猶予（秒）。段差・坂のちらつき吸収。</summary>
        private const float GroundedGraceSeconds = 0.15f;

        /// <summary>キャスト結果の使い回しバッファ（毎TickのGCゼロ。メインスレッド専用）。</summary>
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>オーバーラップの使い回しバッファ。</summary>
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        /// <summary>
        /// クエリ対象レイヤー。Ignore Raycast（layer 2）も**含める**——プレイヤーの体は
        /// 足IKレイの自己ヒット回避のため layer 2 に居るが、キャラ同士は互いを遮るべき。
        /// 自己除外はレイヤーではなく Rigidbody 比較で行う（対称な衝突になる）。
        /// </summary>
        private static readonly int QueryMask = Physics.DefaultRaycastLayers | (1 << 2);

        /// <summary>実体のカプセル。</summary>
        private readonly CapsuleCollider _capsule;

        /// <summary>実体の Rigidbody（kinematic）。クエリの自己除外の鍵にも使う。</summary>
        private readonly Rigidbody _rigidbody;

        /// <summary>Pose原点からカプセル中心までの高さ（足元原点=カプセル半分 / 中心原点=0）。</summary>
        private readonly float _centerY;

        /// <summary>重力（m/s²。実物理より強めがゲームの定番＝落下のキレを出す）。</summary>
        public float Gravity { get; set; } = -22f;

        /// <summary>ジャンプ初速（m/s。頂点高さ ≒ 初速²/2g ≒ 1.0m）。</summary>
        public float JumpSpeed { get; set; } = 6.5f;

        /// <summary>
        /// 接地中か（アニメ切替・足IKの効き・ジャンプ可否の判断材料）。
        /// 生の接触判定は段差・坂で毎フレームちらつくため、
        /// **足元の球判定＋離地の猶予時間**で安定化した値を返す。
        /// </summary>
        public bool IsGrounded { get; private set; } = true;

        /// <summary>垂直速度（m/s）。</summary>
        public float VerticalVelocity { get; private set; }

        /// <summary>今Tickのdt（PreTick で供給）。</summary>
        private float _deltaTime;

        /// <summary>ジャンプ予約（次の Move で消費）。</summary>
        private bool _jumpRequested;

        /// <summary>このTickで Move が呼ばれたか（Idle 中の重力適用の判断に使う）。</summary>
        private bool _movedThisTick;

        /// <summary>最後に接地してからの経過時間。</summary>
        private float _timeSinceGrounded;

        /// <summary>
        /// モーターを生成する（CapsuleCollider と kinematic Rigidbody を host へ装着・設定する）。
        /// originAtFeet はモデルの原点が足元か（Humanoid モデル=true / 中心原点のプリミティブ=false）。
        /// </summary>
        public Sample_KinematicMotor(GameObject host, float height, bool originAtFeet,
            float radius = 0.3f)
        {
            _capsule = host.GetComponent<CapsuleCollider>();
            if (_capsule == null)
            {
                _capsule = host.AddComponent<CapsuleCollider>();
            }
            _capsule.height = Mathf.Max(height * 0.95f, radius * 2.1f);
            _capsule.radius = radius;
            // 足元原点: カプセルの**底**を原点に合わせる（height*0.95 を上下対称に
            // 縮めて中心を身長半分に置くと底が浮き、立ち姿勢で足首が床へ沈んで見える）
            _centerY = originAtFeet ? _capsule.height * 0.5f : 0f;
            _capsule.center = new Vector3(0f, _centerY, 0f);

            _rigidbody = host.GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = host.AddComponent<Rigidbody>();
            }
            _rigidbody.isKinematic = true;   // 位置の真実は Pose（Tick 同期）——物理には委ねない
            _rigidbody.useGravity = false;   // 重力はこのモーターが積分する
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>毎Tickの最初に dt を受け取る（TickPipeline の Simulation 先頭で呼ぶ）。</summary>
        public void PreTick(float deltaTime)
        {
            _deltaTime = deltaTime;
            _movedThisTick = false;
        }

        /// <summary>ジャンプを予約する（接地中のみ受理。戻り値=跳べたか）。</summary>
        public bool RequestJump()
        {
            if (!IsGrounded)
            {
                return false;
            }
            _jumpRequested = true;
            return true;
        }

        /// <summary>このTickに移動処理が走ったかを読み、旗を消費する（Idle重力の判断用）。</summary>
        public bool ConsumeMovedThisTick()
        {
            var moved = _movedThisTick;
            _movedThisTick = false;
            return moved;
        }

        /// <summary>水平の希望変位＋内部の垂直状態（重力・ジャンプ）で実移動を解決する。</summary>
        public Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta)
        {
            _movedThisTick = true;

            // 2D立ち絵へ切替中（実体が非アクティブ）は素通し（従来仕様の踏襲）。
            // 予約は捨てる——2D中に押した Space を3D復帰の初回 Move で暴発させない
            if (_capsule == null || !_capsule.gameObject.activeInHierarchy)
            {
                _jumpRequested = false;
                return currentPosition + desiredDelta;
            }

            var position = currentPosition;

            if (_jumpRequested)
            {
                _jumpRequested = false;
                VerticalVelocity = JumpSpeed;
                IsGrounded = false;
            }
            VerticalVelocity += Gravity * _deltaTime;
            if (IsGrounded && VerticalVelocity < -2f)
            {
                VerticalVelocity = -2f; // 接地中は軽く押し付けるだけ（坂・階段の吸着）
            }

            position = SlideHorizontal(position, new Vector3(desiredDelta.x, 0f, desiredDelta.z));
            position = MoveVertical(position, VerticalVelocity * _deltaTime);
            position = Depenetrate(position);

            // 接地の安定化: 足元の球判定 → さらに離地へ猶予を持たせる。
            // 生値のままだと「一瞬空中→滞空ポーズ→接地→…」の高速往復が姿勢のガクつきになる
            var raw = ProbeGround(position);
            if (raw)
            {
                _timeSinceGrounded = 0f;
            }
            else
            {
                _timeSinceGrounded += _deltaTime;
            }
            // 上昇中（ジャンプ直後）は即座に空中扱い、下降側は猶予内なら接地扱い
            IsGrounded = VerticalVelocity > 0.1f
                ? false
                : raw || _timeSinceGrounded < GroundedGraceSeconds;
            if (IsGrounded && VerticalVelocity < 0f)
            {
                VerticalVelocity = -2f;
            }

            _rigidbody.position = position; // 実体を確定位置へ（Avatar も同じ Transform へ写す）
            return position;
        }

        /// <summary>水平変位を壁ずり＋段差乗り越えで解決する。</summary>
        private Vector3 SlideHorizontal(Vector3 position, Vector3 delta)
        {
            for (var i = 0; i < MaxSlideIterations; i++)
            {
                var distance = delta.magnitude;
                if (distance < 1e-5f)
                {
                    break;
                }
                var direction = delta / distance;
                if (!CastCapsule(position, direction, distance + SkinWidth, out var hit))
                {
                    position += delta;
                    break;
                }
                var travel = Mathf.Max(hit.distance - SkinWidth, 0f);
                position += direction * travel;
                var remaining = direction * (distance - travel);

                if (hit.normal.y < SlopeLimitY)
                {
                    // 壁状の面: まず段差として乗れるか試し、ダメなら壁ずり
                    if (IsGrounded && TryStepUp(position, direction, out var lifted))
                    {
                        // 持ち上げるだけ——残り変位は次の反復がそのまま進める
                        // （前進量を勝手に増やさない＝テレポート感を出さない）
                        position = lifted;
                        delta = remaining;
                        continue;
                    }
                    delta = Vector3.ProjectOnPlane(remaining, hit.normal);
                    delta.y = Mathf.Min(delta.y, 0f); // 壁を駆け上がらない
                }
                else
                {
                    // 登れる坂: 面に沿わせたまま登る（+Y成分を残す）
                    delta = Vector3.ProjectOnPlane(remaining, hit.normal);
                }
            }
            return position;
        }

        /// <summary>
        /// 段差乗り越えを試す: 壁のすぐ先の床の高さをレイ1本で測り、
        /// 乗れる高さ（StepOffset 以内・水平面）で、持ち上げた位置にカプセルが
        /// 収まるなら、そこまで持ち上げる（前進は呼び出し側の残り変位が行う）。
        /// カプセルキャストで測らないのは、下端の丸みが段の**角**に触れて
        /// 法線が斜めになり、登坂限界チェックで正当な段差まで弾かれるため。
        /// </summary>
        private bool TryStepUp(Vector3 position, Vector3 direction, out Vector3 lifted)
        {
            lifted = position;

            // 壁の少し先へ、乗り越え上限の高さから下向きにレイを落とす。
            // 高い壁の場合は origin が壁の中＝ヒット無しで自然に不成立になる
            var probe = position + direction * (_capsule.radius + 0.06f)
                + Vector3.up * (StepOffset + 0.01f);
            if (!RaycastFiltered(probe, Vector3.down, StepOffset, out var top))
            {
                return false;
            }
            if (top.normal.y < SlopeLimitY)
            {
                return false; // 乗る先が急斜面（壁の斜め上面など）
            }
            var stepHeight = top.point.y - position.y;
            if (stepHeight < 0.02f || stepHeight > StepOffset - 0.01f)
            {
                return false; // 実質フラット／高すぎる
            }

            // 持ち上げた位置にカプセルが収まるか（低い天井・奥の壁のチェック）
            var candidate = position + Vector3.up * (stepHeight + SkinWidth);
            if (OverlapsAnything(candidate))
            {
                return false;
            }
            lifted = candidate;
            return true;
        }

        /// <summary>垂直変位（重力・ジャンプ）を解決する。</summary>
        private Vector3 MoveVertical(Vector3 position, float deltaY)
        {
            if (Mathf.Abs(deltaY) < 1e-6f)
            {
                return position;
            }
            var direction = deltaY > 0f ? Vector3.up : Vector3.down;
            var distance = Mathf.Abs(deltaY);
            if (CastCapsule(position, direction, distance + SkinWidth, out var hit))
            {
                position += direction * Mathf.Max(hit.distance - SkinWidth, 0f);
                if (deltaY > 0f)
                {
                    VerticalVelocity = Mathf.Min(VerticalVelocity, 0f); // 天井に頭をぶつけた
                }
            }
            else
            {
                position += direction * distance;
            }
            return position;
        }

        /// <summary>めり込みからの押し出し（丸め誤差・外部からの割り込みの保険）。</summary>
        private Vector3 Depenetrate(Vector3 position)
        {
            GetCapsulePoints(position, out var bottom, out var top);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top,
                _capsule.radius - SkinWidth * 0.5f, OverlapBuffer,
                QueryMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                if (other.attachedRigidbody == _rigidbody)
                {
                    continue; // 自分は無視
                }
                if (Physics.ComputePenetration(
                        _capsule, position, _capsule.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out var direction, out var depth))
                {
                    position += direction * (depth + 0.001f);
                }
            }
            return position;
        }

        /// <summary>足元に地面があるか（球オーバーラップ。自分は除外）。</summary>
        private bool ProbeGround(Vector3 position)
        {
            var height = Mathf.Max(_capsule.height, _capsule.radius * 2f);
            var bottomSphere = position + Vector3.up * (_centerY - height * 0.5f + _capsule.radius);
            var probe = bottomSphere + Vector3.down * (SkinWidth + 0.04f);
            var count = Physics.OverlapSphereNonAlloc(probe, _capsule.radius * 0.95f, OverlapBuffer,
                QueryMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (OverlapBuffer[i].attachedRigidbody != _rigidbody)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>自分を除いた最近傍ヒットを探すカプセルキャスト。</summary>
        private bool CastCapsule(Vector3 position, Vector3 direction, float distance,
            out RaycastHit nearest)
        {
            GetCapsulePoints(position, out var bottom, out var top);
            var count = Physics.CapsuleCastNonAlloc(bottom, top,
                _capsule.radius - SkinWidth * 0.5f, direction, HitBuffer, distance,
                QueryMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (hit.collider.attachedRigidbody == _rigidbody)
                {
                    continue; // 自分は無視（レイヤーに頼らない自己除外）
                }
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>自分を除いた最近傍ヒットを探すレイキャスト。</summary>
        private bool RaycastFiltered(Vector3 origin, Vector3 direction, float distance,
            out RaycastHit nearest)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, distance,
                QueryMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (hit.collider.attachedRigidbody == _rigidbody)
                {
                    continue;
                }
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>仮の位置にカプセルを置いたとき、他者と重なるか。</summary>
        private bool OverlapsAnything(Vector3 position)
        {
            GetCapsulePoints(position, out var bottom, out var top);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top,
                _capsule.radius - SkinWidth * 0.5f, OverlapBuffer,
                QueryMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (OverlapBuffer[i].attachedRigidbody != _rigidbody)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>仮の位置 position に立てた場合のカプセル両端球の中心を求める。</summary>
        private void GetCapsulePoints(Vector3 position, out Vector3 bottom, out Vector3 top)
        {
            var height = Mathf.Max(_capsule.height, _capsule.radius * 2f);
            var center = position + Vector3.up * _centerY;
            var half = Mathf.Max(height * 0.5f - _capsule.radius, 0f);
            bottom = center - Vector3.up * half;
            top = center + Vector3.up * half;
        }
    }
}
