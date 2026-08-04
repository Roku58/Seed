using System;

namespace Seed.Character
{
    /// <summary>
    /// ユニット内で Actor（表現形態）を指す軽量ID。
    /// 「3Dモデル」「2D立ち絵」など、同一キャラクターの複数の表現を
    /// CharacterAgent の中で区別するために使う（値の割り当てはアプリ側）。
    /// </summary>
    public readonly struct ActorKey : IEquatable<ActorKey>
    {
        /// <summary>「Actorなし」を表す予約値。</summary>
        public static readonly ActorKey None = new ActorKey(0);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>ActorKey を生成する。</summary>
        public ActorKey(int value)
        {
            Value = value;
        }

        /// <summary>同一キーかを返す。</summary>
        public bool Equals(ActorKey other)
        {
            return Value == other.Value;
        }

        /// <summary>同一キーかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is ActorKey other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Actor#{Value}";
        }
    }
}
