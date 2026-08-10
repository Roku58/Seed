using System.Collections.Generic;
using Seed.Character;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// AnimatorController 駆動の Avatar（IAvatar 実装。RiggedAvatar と対になる選択肢）。
    ///
    /// [役割分担] 行動の真実は Behavior 状態機械（Seed.Character）が持ち、本クラスは
    /// 遷移通知を**ステート名へのクロスフェード命令**へ翻訳するだけ。Controller の
    /// グラフには遷移条件・Exit Time のロジックを持たせない——Controller は
    /// 「ステートとブレンドツリーの置き場」として使う。状態機械が2つあると必ずズレる
    /// ため、真実を一箇所に保ったまま Controller の実利（ブレンドツリー・ツール・
    /// アニメーター職との協業）だけを得るための使い方。
    ///
    /// [使い分け] 単純なクリップ切替なら RiggedAvatar（Playables 直駆動＝
    /// モーション追加が MotionSet.Add 1行）、ブレンドツリーやレイヤー合成が要るなら本クラス。
    ///
    /// [逆流] クリップの AnimationEvent（int 引数）→ OnAnimationEvent → IAvatarEventSink。
    /// ゲームロジックが Animator の状態を読むことは禁止（CODING_STANDARDS §6）。
    /// </summary>
    public sealed class AnimatorAvatar : MonoBehaviour, IAvatar
    {
        /// <summary>再生器（Unity の Animator。Controller はステート置き場として使う）。</summary>
        private Animator _animator;

        /// <summary>行動キー→ステートの対応表。</summary>
        private AnimatorAvatarBindings _bindings;

        /// <summary>アニメーションイベントの戻し先（Actor）。</summary>
        private IAvatarEventSink _eventSink;

        /// <summary>移動ブレンド用パラメータのハッシュ。</summary>
        private int _speedParameterHash;

        /// <summary>
        /// 依存を差し込む（生成直後に一度だけ）。speedParameter はブレンドツリーが読む
        /// 正規化速度（0〜1）の Animator パラメータ名。
        /// </summary>
        public void Configure(Animator animator, AnimatorAvatarBindings bindings = null,
            string speedParameter = "Speed")
        {
            _animator = animator;
            _bindings = bindings ?? AnimatorAvatarBindings.Default;
            _speedParameterHash = Animator.StringToHash(speedParameter);

            // 位置の真実は ActorPose——ルートモーションは使わない（09章の規約）
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>
        /// Animator 本体。App が追加のブレンドパラメータを**書く**ための公開であり、
        /// 状態（GetCurrentAnimatorStateInfo 等）を**読む**のは禁止——真実は Behavior にある。
        /// </summary>
        public Animator Animator => _animator;

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

        /// <summary>行動遷移をステートへのクロスフェード命令へ翻訳する。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
            if (_animator == null || !_bindings.TryResolve(next, out var state))
            {
                return; // 未登録の行動は演出なし（現在のステートを保つ＝安全側）
            }
            _animator.CrossFadeInFixedTime(state.StateHash, state.FadeSeconds, state.Layer);
        }

        /// <summary>移動の正規化速度をブレンドパラメータへ写す（ブレンドツリーが読む）。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            if (_animator != null)
            {
                _animator.SetFloat(_speedParameterHash, normalizedSpeed);
            }
        }

        /// <summary>アニメーションイベントの戻し先を差し込む（Actor が自分を渡す）。</summary>
        public void BindEventSink(IAvatarEventSink sink)
        {
            _eventSink = sink;
        }

        /// <summary>表示物を解放する（GameObject 破棄）。</summary>
        public void Release()
        {
            _eventSink = null;
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
    }

    /// <summary>
    /// 行動キー → Animator ステートの対応表。
    /// 既定は BehaviorKey と同名のステート（Idle→"Idle" 等）へ 0.15 秒フェード。
    /// ステート名・フェード秒・レイヤーを変えたい場合だけ Bind で上書きする。
    /// </summary>
    public sealed class AnimatorAvatarBindings
    {
        /// <summary>1件ぶんの対応（ステート名ハッシュ・フェード秒・レイヤー）。</summary>
        public readonly struct Entry
        {
            /// <summary>ステート名のハッシュ（Animator.StringToHash）。</summary>
            public readonly int StateHash;

            /// <summary>クロスフェード秒。</summary>
            public readonly float FadeSeconds;

            /// <summary>対象レイヤー。</summary>
            public readonly int Layer;

            /// <summary>Entry を生成する。</summary>
            public Entry(int stateHash, float fadeSeconds, int layer)
            {
                StateHash = stateHash;
                FadeSeconds = fadeSeconds;
                Layer = layer;
            }
        }

        /// <summary>対応表本体。</summary>
        private readonly Dictionary<BehaviorKey, Entry> _map =
            new Dictionary<BehaviorKey, Entry>();

        /// <summary>標準6行動を同名ステートへ束ねた既定の対応表。</summary>
        public static AnimatorAvatarBindings Default => new AnimatorAvatarBindings()
            .Bind(BehaviorKey.Idle, "Idle")
            .Bind(BehaviorKey.Locomotion, "Locomotion")
            .Bind(BehaviorKey.Attack, "Attack", 0.08f)
            .Bind(BehaviorKey.Guard, "Guard")
            .Bind(BehaviorKey.Hit, "Hit", 0.05f)
            .Bind(BehaviorKey.Death, "Death");

        /// <summary>対応を追加・上書きする（メソッドチェーン可）。</summary>
        public AnimatorAvatarBindings Bind(BehaviorKey key, string stateName,
            float fadeSeconds = 0.15f, int layer = 0)
        {
            _map[key] = new Entry(Animator.StringToHash(stateName), fadeSeconds, layer);
            return this;
        }

        /// <summary>行動キーの対応を引く（未登録なら false）。</summary>
        public bool TryResolve(BehaviorKey key, out Entry entry)
        {
            return _map.TryGetValue(key, out entry);
        }
    }
}
