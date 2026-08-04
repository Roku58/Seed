using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// ユニット1体ぶんのAI記憶（黒板）。
    /// 「直近の攻撃時刻」「警戒度」「狙っている相手」など、思考の継続に必要な値を
    /// int キーで置く（キーの割り当てはアプリの定数クラス。ScreenId と同じ流儀）。
    ///
    /// TimeSeconds は AiBrain が毎 Think で進めるAI時計——クールダウンは
    /// 「発動時刻を覚えて経過を比べる」形で書ける（デルタ加算の分散を防ぐ）。
    /// </summary>
    public sealed class AiBlackboard
    {
        /// <summary>数値の記憶。</summary>
        private readonly Dictionary<int, float> _floats = new Dictionary<int, float>();

        /// <summary>ID・カウンタの記憶。</summary>
        private readonly Dictionary<int, int> _ints = new Dictionary<int, int>();

        /// <summary>AI時計（秒。AiBrain が Think のたびに進める）。</summary>
        public float TimeSeconds { get; internal set; }

        /// <summary>数値を読む（未設定は既定値）。</summary>
        public float GetFloat(int key, float defaultValue = 0f)
        {
            return _floats.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>数値を書く。</summary>
        public void SetFloat(int key, float value)
        {
            _floats[key] = value;
        }

        /// <summary>整数を読む（未設定は既定値）。</summary>
        public int GetInt(int key, int defaultValue = 0)
        {
            return _ints.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>整数を書く。</summary>
        public void SetInt(int key, int value)
        {
            _ints[key] = value;
        }

        /// <summary>キャラクターIDを読む（未設定は None）。</summary>
        public CharacterId GetCharacter(int key)
        {
            return new CharacterId(GetInt(key));
        }

        /// <summary>キャラクターIDを書く。</summary>
        public void SetCharacter(int key, CharacterId id)
        {
            SetInt(key, id.Value);
        }

        /// <summary>全記憶を消す（プール再利用・リスポーン時）。</summary>
        public void Clear()
        {
            _floats.Clear();
            _ints.Clear();
            TimeSeconds = 0f;
        }
    }
}
