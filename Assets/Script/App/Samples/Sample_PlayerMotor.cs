using Seed.Character;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】プレイヤー移動モーター（App の方針実装。IMotionSolver）。
    ///
    /// CharacterController・重力・ジャンプ・段差乗り越えを1か所に集約する。
    /// 以前は「平地=地面吸着ソルバー / 生成ステージ=CharacterController」の二本立てだったが、
    /// 重力が無い CharacterController は接地せず段差に引っかかる（浮いたまま蹴上げへ
    /// 水平衝突して登れない）ため、**常に重力をかけ続ける一本のモーター**へ統一した。
    /// 段差は CharacterController の stepOffset が処理する＝階段・坂・穴すべて標準挙動になる。
    ///
    /// [規約との関係] Behavior は水平の希望変位だけを出し、垂直（重力・ジャンプ）は
    /// このモーターの内部状態。位置の真実は ActorPose のまま——Move の戻り値が Pose へ入る。
    /// dt は毎Tick <see cref="PreTick"/> で受け取る（IMotionSolver の契約に dt が無いため）。
    /// </summary>
    public sealed class Sample_PlayerMotor : IMotionSolver
    {
        /// <summary>実体（壁・段差との衝突解決者）。</summary>
        private readonly CharacterController _controller;

        /// <summary>重力（m/s²。実物理より強めがゲームの定番＝落下のキレを出す）。</summary>
        public float Gravity { get; set; } = -22f;

        /// <summary>ジャンプ初速（m/s。頂点高さ ≒ 初速²/2g ≒ 1.0m）。</summary>
        public float JumpSpeed { get; set; } = 6.5f;

        /// <summary>
        /// 接地中か（アニメ切替・足IKの効き・ジャンプ可否の判断材料）。
        /// CharacterController.isGrounded は走行中・段差・坂で毎フレームちらつくため、
        /// **足元の球判定＋離地の猶予時間**で安定化した値を返す（Starter Assets と同じ手法）。
        /// これが生値のままだと「一瞬空中→ジャンプポーズ→接地→…」の高速往復が
        /// のけぞり姿勢のガクつきとして見える。
        /// </summary>
        public bool IsGrounded { get; private set; } = true;

        /// <summary>離地をなかったことにする猶予（秒）。段差・坂のちらつき吸収。</summary>
        private const float GroundedGraceSeconds = 0.15f;

        /// <summary>足元球判定の半径（コライダーよりわずかに細く）。</summary>
        private float _probeRadius;

        /// <summary>最後に接地してからの経過時間。</summary>
        private float _timeSinceGrounded;

        /// <summary>垂直速度（m/s）。</summary>
        public float VerticalVelocity { get; private set; }

        /// <summary>今Tickのdt（PreTick で供給）。</summary>
        private float _deltaTime;

        /// <summary>ジャンプ予約（次の Move で消費）。</summary>
        private bool _jumpRequested;

        /// <summary>このTickで Move が呼ばれたか（Idle 中の重力適用の判断に使う）。</summary>
        private bool _movedThisTick;

        /// <summary>
        /// モーターを生成する（CharacterController は host へ装着・設定される）。
        /// originAtFeet はモデルの原点が足元か（Humanoid モデル=true / 中心原点のカプセル=false）。
        /// </summary>
        public Sample_PlayerMotor(GameObject host, float height, bool originAtFeet,
            float radius = 0.3f)
        {
            _controller = host.GetComponent<CharacterController>();
            if (_controller == null)
            {
                _controller = host.AddComponent<CharacterController>();
            }
            _controller.height = Mathf.Max(height * 0.95f, radius * 2.1f);
            _controller.radius = radius;
            _controller.center = originAtFeet
                ? new Vector3(0f, height * 0.5f, 0f)
                : Vector3.zero;
            _controller.stepOffset = 0.4f;   // 蹴上げ0.18mの階段を確実に登れる高さ
            _controller.slopeLimit = 50f;    // デモの12度坂は余裕・急壁は登れない
            _controller.skinWidth = 0.03f;
            _controller.minMoveDistance = 0f;
            _probeRadius = radius * 0.95f;
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

            // 2D立ち絵へ切替中（実体が非アクティブ）は素通し（従来仕様の踏襲）
            if (_controller == null || !_controller.gameObject.activeInHierarchy)
            {
                return currentPosition + desiredDelta;
            }

            // 位置の真実は Pose——原点回帰などで Pose が動いた場合も実体を追従させる
            if (_controller.transform.position != currentPosition)
            {
                _controller.transform.position = currentPosition;
            }

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

            var delta = new Vector3(desiredDelta.x, 0f, desiredDelta.z)
                + Vector3.up * (VerticalVelocity * _deltaTime);
            _controller.Move(delta);

            // 接地の安定化: CCの生値 or 足元の球判定 → さらに離地へ猶予を持たせる
            var feet = _controller.transform.position
                + _controller.center - Vector3.up * (_controller.height * 0.5f - _probeRadius + 0.06f);
            var raw = _controller.isGrounded
                || Physics.CheckSphere(feet, _probeRadius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
            return _controller.transform.position;
        }
    }
}
