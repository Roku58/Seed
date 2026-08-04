using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// ゲームフェーズ（ホーム・戦闘・ショップ…）を指す共有ID。
    /// フェーズは「ゲームごとに増える語彙」なので enum ではなく int 値型にしてある
    /// （値の割り当てはアプリ側の定数クラス。ScreenId と同じ流儀）。
    /// </summary>
    public readonly struct PhaseId : IEquatable<PhaseId>
    {
        /// <summary>「フェーズなし」を表す予約値（起動直後・遷移中）。</summary>
        public static readonly PhaseId None = new PhaseId(0);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>PhaseId を生成する。</summary>
        public PhaseId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(PhaseId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is PhaseId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Phase#{Value}";
        }
    }
}
