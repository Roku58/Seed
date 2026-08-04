using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 陣営ID（プレイヤー・エネミー・中立…）。
    /// 「陣営はゲームごとに増える語彙」なので enum ではなく int 値型にしてある
    /// （3陣営以上の対戦・寝返り・共闘を契約変更なしで表現できる）。
    /// 予約: 1=Players / 2=Enemies。アプリ独自は 3 以降。
    /// </summary>
    public readonly struct FactionId : IEquatable<FactionId>
    {
        /// <summary>「無所属・不在」を表す予約値。</summary>
        public static readonly FactionId None = new FactionId(0);

        /// <summary>プレイヤー陣営。</summary>
        public static readonly FactionId Players = new FactionId(1);

        /// <summary>エネミー陣営。</summary>
        public static readonly FactionId Enemies = new FactionId(2);

        /// <summary>問い合わせ用のワイルドカード（登録には使わない）。</summary>
        public static readonly FactionId Any = new FactionId(-1);

        /// <summary>ID値。</summary>
        public readonly int Value;

        /// <summary>FactionId を生成する。</summary>
        public FactionId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(FactionId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is FactionId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Faction#{Value}";
        }
    }
}
