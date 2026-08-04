using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// キャラクターに依頼するリアクションの種類ID（PlayReactionCommand で使う）。
    ///
    /// enum ではなく int 値型にしてあるのは、リアクションの種類が
    /// 「ゲームごとに増える語彙」だから。基盤の契約に手を入れずに
    /// アプリ側が独自リアクション（よろけ・打ち上げ・凍結など）を追加でき、
    /// 受け側（キャラクター基盤）はリアクションID→行動の対応表に1行足すだけで済む。
    ///
    /// 予約: 1〜99 は基盤の標準リアクション。アプリ独自は 100 以降を推奨。
    /// 同じ流儀の値型ID: <see cref="ScreenId"/> / <see cref="CharacterId"/>。
    /// </summary>
    public readonly struct ReactionId : IEquatable<ReactionId>
    {
        /// <summary>「なし」を表す予約値。</summary>
        public static readonly ReactionId None = new ReactionId(0);

        /// <summary>攻撃の振り。</summary>
        public static readonly ReactionId Attack = new ReactionId(1);

        /// <summary>被弾のけぞり。</summary>
        public static readonly ReactionId Hit = new ReactionId(2);

        /// <summary>ガード構えの開始。</summary>
        public static readonly ReactionId GuardOn = new ReactionId(3);

        /// <summary>ガード構えの解除。</summary>
        public static readonly ReactionId GuardOff = new ReactionId(4);

        /// <summary>戦闘不能（倒れ）。</summary>
        public static readonly ReactionId Death = new ReactionId(5);

        /// <summary>ID値（1以上が有効。アプリ独自は100以降を推奨）。</summary>
        public readonly int Value;

        /// <summary>ReactionId を生成する。</summary>
        public ReactionId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(ReactionId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is ReactionId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Reaction#{Value}";
        }
    }
}
