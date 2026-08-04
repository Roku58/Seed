using Seed.Character;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 本格3Dモデル用の Avatar（アニメーション制御＋姿勢/IK制御つき。アクションゲーム想定）。
    ///
    /// [役割分担]
    /// - 行動の真実 … Behavior 状態機械（Seed.Character）が持つ。本クラスは遷移通知を
    ///   受けて MotionSet のクリップをクロスフェード再生するだけ（状態機械の二重化をしない）
    /// - 当たり判定の窓・コンボ受付 … 正規化‰イベント or クリップの AnimationEvent が
    ///   IAvatarEventSink 経由で行動側へ還流する（演出の時間軸→論理の時間軸の唯一の逆流）
    /// - 姿勢・IK … MotionRig（LateUpdate）がアニメの上へ重ねる
    ///
    /// [使い方] モデルの GameObject に付け、Configure(animator, motionSet, bindings) を呼ぶ。
    /// AnimatorController アセットは不要（Playables 直駆動）。
    /// モーションの追加＝MotionSet.Add 1行＋対応表1行。
    /// </summary>
    public sealed class RiggedAvatar : MonoBehaviour, IAvatar
    {
        /// <summary>再生器。</summary>
        private AnimationDriver _driver;

        /// <summary>行動キー→モーションの対応表（同値素通しの既定つき）。</summary>
        private MotionBindings _bindings;

        /// <summary>アニメーションイベントの戻し先（Actor）。</summary>
        private IAvatarEventSink _eventSink;

        /// <summary>移動ブレンド用に速度を憶える（Locomotion 再生速度に反映）。</summary>
        private float _locomotionSpeed;

        /// <summary>
        /// 依存を差し込む（生成直後に一度だけ）。upperBodyMask は上半身レイヤー用（任意）。
        /// </summary>
        public void Configure(Animator animator, MotionSet motionSet,
            MotionBindings bindings = null, AvatarMask upperBodyMask = null)
        {
            _bindings = bindings ?? MotionBindings.Default;
            _driver = new AnimationDriver(motionSet);
            _driver.Initialize(animator, upperBodyMask);
            _driver.MotionEventFired += eventId => _eventSink?.PostAvatarEvent(eventId);
        }

        /// <summary>再生器（上半身レイヤー等の直接操作用）。</summary>
        public AnimationDriver Driver => _driver;

        /// <summary>表示を立てる/降ろす。</summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>姿勢をルートの Transform へ反映する（真実は ActorPose）。</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>行動遷移をモーションのクロスフェードへ翻訳する。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
            if (_driver == null)
            {
                return;
            }
            var clip = _bindings.Resolve(next);
            if (!clip.Equals(MotionClipId.None))
            {
                _driver.Play(clip);
            }
        }

        /// <summary>移動の正規化速度（Locomotion クリップの再生速度ブレンドに使える）。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            _locomotionSpeed = normalizedSpeed;
        }

        /// <summary>アニメーションイベントの戻し先を差し込む（Actor が自分を渡す）。</summary>
        public void BindEventSink(IAvatarEventSink sink)
        {
            _eventSink = sink;
        }

        /// <summary>表示物を解放する（グラフ破棄→GameObject破棄）。</summary>
        public void Release()
        {
            _eventSink = null;
            _driver?.Dispose();
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>クリップに仕込んだ AnimationEvent の受信口（int 引数＝AvatarEventId 値）。</summary>
        public void OnAnimationEvent(int eventId)
        {
            _eventSink?.PostAvatarEvent(eventId);
        }

        /// <summary>再生を進める（艶はUpdateの規約。グラフは手動評価）。</summary>
        private void Update()
        {
            _driver?.Tick(Time.deltaTime);
        }

        /// <summary>グラフの破棄漏れ防止。</summary>
        private void OnDestroy()
        {
            _driver?.Dispose();
        }
    }

    /// <summary>
    /// 行動キー→モーションの対応表。
    /// 既定は「同じ値へ素通し」（BehaviorKey.Attack(3)→MotionClipId(3)）なので、
    /// 番号を揃えて運用すれば対応表を書く必要すらない。ズレる場合だけ Bind する。
    /// </summary>
    public sealed class MotionBindings
    {
        /// <summary>既定の対応表（同値素通しのみ）。</summary>
        public static readonly MotionBindings Default = new MotionBindings();

        /// <summary>明示の対応。</summary>
        private readonly System.Collections.Generic.Dictionary<int, MotionClipId> _map =
            new System.Collections.Generic.Dictionary<int, MotionClipId>();

        /// <summary>対応を明示する（流れるように書ける糖衣）。</summary>
        public MotionBindings Bind(BehaviorKey behavior, MotionClipId clip)
        {
            _map[behavior.Value] = clip;
            return this;
        }

        /// <summary>行動キーからモーションを引く（明示が無ければ同値素通し）。</summary>
        public MotionClipId Resolve(BehaviorKey behavior)
        {
            return _map.TryGetValue(behavior.Value, out var clip)
                ? clip
                : new MotionClipId(behavior.Value);
        }
    }
}
