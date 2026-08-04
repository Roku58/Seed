using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Seed.Motion
{
    /// <summary>
    /// Playables 直駆動のアニメーション再生器（AnimatorController アセット不要）。
    ///
    /// [構成] AnimationPlayableOutput → LayerMixer（0=全身 / 1=上半身オーバーレイ）
    ///        → 各層 2スロットの MixerPlayable（クロスフェードのピンポン）
    /// [なぜ Playables か] 遷移の真実は Behavior 状態機械が持っている——
    /// Animator の状態機械を重ねると真実が二重化する。Playables なら
    /// 「今このクリップを何％で流す」だけをコードが完全掌握でき、
    /// モーションの追加はアセット編集でなく MotionSet.Add 1行になる。
    /// [イベント] 正規化‰イベント（CrossfadeState）を onEvent へ発火する。
    /// 上半身レイヤーは AvatarMask（任意）で範囲を絞れる（走りながら上半身だけ攻撃）。
    /// </summary>
    public sealed class AnimationDriver : IDisposable
    {
        /// <summary>1レイヤーぶんの再生系（2スロットのピンポン・クロスフェード）。</summary>
        private sealed class Layer
        {
            /// <summary>スロット2本のミキサー。</summary>
            public AnimationMixerPlayable Mixer;

            /// <summary>各スロットのクリップ再生。</summary>
            public readonly AnimationClipPlayable[] Slots = new AnimationClipPlayable[2];

            /// <summary>現在使用中のスロット番号。</summary>
            public int ActiveSlot;

            /// <summary>フェード・イベントの状態。</summary>
            public readonly CrossfadeState State = new CrossfadeState();

            /// <summary>再生中のモーションID。</summary>
            public MotionClipId Current = MotionClipId.None;
        }

        /// <summary>モーション台帳。</summary>
        private readonly MotionSet _set;

        /// <summary>グラフ本体。</summary>
        private PlayableGraph _graph;

        /// <summary>レイヤー合成（0=全身、1=上半身）。</summary>
        private AnimationLayerMixerPlayable _layerMixer;

        /// <summary>全身レイヤー。</summary>
        private readonly Layer _base = new Layer();

        /// <summary>上半身レイヤー。</summary>
        private readonly Layer _upper = new Layer();

        /// <summary>初期化済みか。</summary>
        private bool _isInitialized;

        /// <summary>AnimationDriver を生成する。</summary>
        public AnimationDriver(MotionSet set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        /// <summary>正規化‰イベントの通知先（AvatarEventId の値体系）。</summary>
        public event Action<int> MotionEventFired;

        /// <summary>全身レイヤーの再生中モーション。</summary>
        public MotionClipId CurrentMotion => _base.Current;

        /// <summary>
        /// グラフを組んで Animator に接続する（AnimatorController は不要。
        /// upperBodyMask を渡すと上半身レイヤーが有効になる）。
        /// </summary>
        public void Initialize(Animator animator, AvatarMask upperBodyMask = null)
        {
            _graph = PlayableGraph.Create("Seed.Motion");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); // Tick 駆動（艶はUpdateの規約）
            var output = AnimationPlayableOutput.Create(_graph, "Motion", animator);

            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            _base.Mixer = AnimationMixerPlayable.Create(_graph, 2);
            _upper.Mixer = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(_base.Mixer, 0, _layerMixer, 0);
            _graph.Connect(_upper.Mixer, 0, _layerMixer, 1);
            _layerMixer.SetInputWeight(0, 1f);
            _layerMixer.SetInputWeight(1, 0f);
            if (upperBodyMask != null)
            {
                _layerMixer.SetLayerMaskFromAvatarMask(1, upperBodyMask);
            }
            _layerMixer.SetLayerAdditive(1, false);

            output.SetSourcePlayable(_layerMixer);
            _graph.Play();
            _isInitialized = true;
        }

        /// <summary>全身レイヤーで再生する（fadeSeconds 省略時は登録の既定）。</summary>
        public void Play(MotionClipId id, float fadeSecondsOverride = -1f)
        {
            PlayOn(_base, id, fadeSecondsOverride);
        }

        /// <summary>上半身レイヤーで再生する（走りながら上半身だけ攻撃、など）。</summary>
        public void PlayUpper(MotionClipId id, float weight = 1f, float fadeSecondsOverride = -1f)
        {
            PlayOn(_upper, id, fadeSecondsOverride);
            if (_isInitialized)
            {
                _layerMixer.SetInputWeight(1, Mathf.Clamp01(weight));
            }
        }

        /// <summary>上半身レイヤーを畳む（全身のみに戻す）。</summary>
        public void ClearUpper()
        {
            if (_isInitialized)
            {
                _layerMixer.SetInputWeight(1, 0f);
            }
            _upper.Current = MotionClipId.None;
        }

        /// <summary>
        /// 時間を進める（RiggedAvatar の Update から呼ばれる＝艶レイヤー）。
        /// フェード重みの適用・正規化イベントの発火・グラフ評価まで行う。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
            {
                return;
            }
            TickLayer(_base, deltaTime, fireEvents: true);
            TickLayer(_upper, deltaTime, fireEvents: false); // イベントの真実は全身レイヤーに一本化
            _graph.Evaluate(deltaTime);
        }

        /// <summary>グラフを破棄する（Avatar の Release から）。</summary>
        public void Dispose()
        {
            if (_isInitialized && _graph.IsValid())
            {
                _graph.Destroy();
            }
            _isInitialized = false;
        }

        /// <summary>1レイヤーへ再生指示を出す。</summary>
        private void PlayOn(Layer layer, MotionClipId id, float fadeSecondsOverride)
        {
            if (!_isInitialized || !_set.TryGet(id, out var entry) || entry.Clip == null)
            {
                return; // 未登録・クリップ無しは静かに何もしない（プリミティブ演出に任せる）
            }
            if (layer.Current.Equals(id) && entry.Descriptor.Loop)
            {
                return; // 同じループモーションは流し直さない
            }

            // 次スロットへ新クリップを差し、フェード開始
            var next = 1 - layer.ActiveSlot;
            if (layer.Slots[next].IsValid())
            {
                _graph.Disconnect(layer.Mixer, next);
                layer.Slots[next].Destroy();
            }
            var playable = AnimationClipPlayable.Create(_graph, entry.Clip);
            playable.SetSpeed(entry.Speed);
            _graph.Connect(playable, 0, layer.Mixer, next);
            layer.Slots[next] = playable;
            layer.ActiveSlot = next;
            layer.Current = id;
            layer.State.Play(in entry.Descriptor,
                fadeSecondsOverride >= 0f ? fadeSecondsOverride : entry.FadeSeconds);
        }

        /// <summary>1レイヤーの重み・イベントを進める。</summary>
        private void TickLayer(Layer layer, float deltaTime, bool fireEvents)
        {
            if (layer.Current.Equals(MotionClipId.None))
            {
                return;
            }
            layer.State.Tick(deltaTime,
                fireEvents ? eventId => MotionEventFired?.Invoke(eventId) : (Action<int>)null);

            var weight = layer.State.CurrentWeight;
            layer.Mixer.SetInputWeight(layer.ActiveSlot, weight);
            layer.Mixer.SetInputWeight(1 - layer.ActiveSlot, 1f - weight);

            // フェード完了後、旧スロットは破棄して空ける
            if (weight >= 1f && layer.Slots[1 - layer.ActiveSlot].IsValid())
            {
                _graph.Disconnect(layer.Mixer, 1 - layer.ActiveSlot);
                layer.Slots[1 - layer.ActiveSlot].Destroy();
            }
        }
    }
}
