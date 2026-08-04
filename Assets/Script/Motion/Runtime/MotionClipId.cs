using System;

namespace Seed.Motion
{
    /// <summary>
    /// モーション（アニメーションクリップの論理名）のID。
    /// 「歩き」「斬り上げ」…モーションはゲームごとに増える語彙なので int 値型
    /// （標準は行動キーと同じ番号を予約し、アプリ独自は100以降を推奨）。
    /// BehaviorKey と同値にしておくと対応表が素通しになる。
    /// </summary>
    public readonly struct MotionClipId : IEquatable<MotionClipId>
    {
        /// <summary>なし。</summary>
        public static readonly MotionClipId None = new MotionClipId(0);

        /// <summary>待機。</summary>
        public static readonly MotionClipId Idle = new MotionClipId(1);

        /// <summary>移動。</summary>
        public static readonly MotionClipId Locomotion = new MotionClipId(2);

        /// <summary>攻撃。</summary>
        public static readonly MotionClipId Attack = new MotionClipId(3);

        /// <summary>ガード。</summary>
        public static readonly MotionClipId Guard = new MotionClipId(4);

        /// <summary>被弾。</summary>
        public static readonly MotionClipId Hit = new MotionClipId(5);

        /// <summary>戦闘不能。</summary>
        public static readonly MotionClipId Death = new MotionClipId(6);

        /// <summary>ID値（アプリ独自は100以降を推奨）。</summary>
        public readonly int Value;

        /// <summary>MotionClipId を生成する。</summary>
        public MotionClipId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(MotionClipId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is MotionClipId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Motion#{Value}";
        }
    }
}
