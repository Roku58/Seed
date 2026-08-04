using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 境界を越えてキャラクターを指すための共有ID（実体参照を渡さないための通貨）。
    /// GameCore 側の EntityRegistry のIDと同じ値を使うと、翻訳が単純になる。
    /// </summary>
    public readonly struct CharacterId : IEquatable<CharacterId>
    {
        /// <summary>「対象なし」を表す予約値。</summary>
        public static readonly CharacterId None = new CharacterId(0);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>CharacterId を生成する。</summary>
        public CharacterId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(CharacterId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is CharacterId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Character#{Value}";
        }
    }
}
