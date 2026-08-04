using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 境界を越えて画面を指すための共有ID。
    /// 画面の実体（プレハブ・クラス）はUI基盤の内側にあり、他の基盤はこのIDしか知らない。
    /// 値の割り当てはアプリ側で定数クラスとして定義する。
    /// </summary>
    public readonly struct ScreenId : IEquatable<ScreenId>
    {
        /// <summary>「画面なし」を表す予約値。</summary>
        public static readonly ScreenId None = new ScreenId(0);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>ScreenId を生成する。</summary>
        public ScreenId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(ScreenId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is ScreenId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Screen#{Value}";
        }
    }
}
