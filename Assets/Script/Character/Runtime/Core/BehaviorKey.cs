using System;

namespace Seed.Character
{
    /// <summary>
    /// 行動（Behavior）の種別ID。
    /// enum にしない理由: 行動は「数だけ用意する」拡張点であり、
    /// アプリ側が基盤に手を入れず独自の行動キーを追加できるようにするため。
    /// 標準キーは 1〜99 を予約し、アプリ独自キーは 100 以降を推奨する。
    /// </summary>
    public readonly struct BehaviorKey : IEquatable<BehaviorKey>
    {
        /// <summary>「遷移なし・行動なし」を表す予約値。</summary>
        public static readonly BehaviorKey None = new BehaviorKey(0);

        /// <summary>待機。</summary>
        public static readonly BehaviorKey Idle = new BehaviorKey(1);

        /// <summary>移動（歩き・走りの総称）。</summary>
        public static readonly BehaviorKey Locomotion = new BehaviorKey(2);

        /// <summary>攻撃。</summary>
        public static readonly BehaviorKey Attack = new BehaviorKey(3);

        /// <summary>ガード（構え続ける継続系）。</summary>
        public static readonly BehaviorKey Guard = new BehaviorKey(4);

        /// <summary>被弾のけぞり。</summary>
        public static readonly BehaviorKey Hit = new BehaviorKey(5);

        /// <summary>戦闘不能（終端。二度と抜けない）。</summary>
        public static readonly BehaviorKey Death = new BehaviorKey(6);

        /// <summary>ID値（1以上が有効。アプリ独自は100以降を推奨）。</summary>
        public readonly int Value;

        /// <summary>BehaviorKey を生成する。</summary>
        public BehaviorKey(int value)
        {
            Value = value;
        }

        /// <summary>同一キーかを返す。</summary>
        public bool Equals(BehaviorKey other)
        {
            return Value == other.Value;
        }

        /// <summary>同一キーかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is BehaviorKey other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Behavior#{Value}";
        }
    }
}
