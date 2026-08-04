using System.Collections.Generic;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// モーションの登録表（MotionClipId → クリップ＋再生仕様）。
    ///
    /// AnimatorController（状態機械アセット）を使わず、コードとデータだけで
    /// アニメーションを管理するための台帳——遷移の真実は Behavior 状態機械が
    /// 既に持っているので、アニメ側に状態機械を二重に作らない、が設計の芯。
    /// イベント（当たり判定窓・足音）は正規化時間‰で登録でき、
    /// **クリップアセットを編集せずに** 行動側へ合図を送れる。
    /// </summary>
    public sealed class MotionSet
    {
        /// <summary>登録1件。</summary>
        public readonly struct Entry
        {
            /// <summary>クリップ実体。</summary>
            public readonly AnimationClip Clip;

            /// <summary>再生仕様（純データ側）。</summary>
            public readonly MotionDescriptor Descriptor;

            /// <summary>再生速度倍率。</summary>
            public readonly float Speed;

            /// <summary>切り替え時の既定フェード秒。</summary>
            public readonly float FadeSeconds;

            /// <summary>Entry を生成する。</summary>
            public Entry(AnimationClip clip, in MotionDescriptor descriptor,
                float speed, float fadeSeconds)
            {
                Clip = clip;
                Descriptor = descriptor;
                Speed = speed;
                FadeSeconds = fadeSeconds;
            }
        }

        /// <summary>登録表。</summary>
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        /// <summary>
        /// モーションを登録する（流れるように書ける糖衣）。
        /// loop 省略時はクリップの isLooping に従う。events は正規化‰昇順で渡すこと。
        /// </summary>
        public MotionSet Add(MotionClipId id, AnimationClip clip,
            float fadeSeconds = 0.15f, float speed = 1f, bool? loop = null,
            params MotionEvent[] events)
        {
            var descriptor = new MotionDescriptor(
                clip != null ? clip.length : 0.001f,
                loop ?? (clip != null && clip.isLooping),
                events);
            _entries[id.Value] = new Entry(clip, in descriptor, speed, fadeSeconds);
            return this;
        }

        /// <summary>登録を引く。</summary>
        public bool TryGet(MotionClipId id, out Entry entry)
        {
            return _entries.TryGetValue(id.Value, out entry);
        }
    }
}
