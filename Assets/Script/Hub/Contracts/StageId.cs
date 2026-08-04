using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// ステージ（マップ・アリーナ・面）を指す共有ID。
    /// マスターデータの定義ID（IEntityDefinition.Id）と同じ値を使うと素通しで引ける。
    /// ステージ切り替えは「戦闘フェーズへ StageId を荷物にして再入する」のが標準経路
    /// （ChangePhaseCommand.Payload に Value を載せる）。
    /// </summary>
    public readonly struct StageId : IEquatable<StageId>
    {
        /// <summary>「ステージなし・指定なし」を表す予約値。</summary>
        public static readonly StageId None = new StageId(0);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>StageId を生成する。</summary>
        public StageId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(StageId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is StageId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Stage#{Value}";
        }
    }
}
