using System;

namespace Seed.StageGen
{
    /// <summary>
    /// バイオーム（領域テーマ）のID。草原・溶岩・市場…値の割り当てはアプリ側。
    /// 素材バリアントの重み表・敵の出現テーブル・施工パレットの分岐キーになる。
    /// </summary>
    public readonly struct BiomeId : IEquatable<BiomeId>
    {
        /// <summary>テーマなし（既定素材が使われる）。</summary>
        public static readonly BiomeId None = new BiomeId(0);

        /// <summary>ID値。</summary>
        public readonly int Value;

        /// <summary>BiomeId を生成する。</summary>
        public BiomeId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(BiomeId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is BiomeId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Biome#{Value}";
        }
    }
}
