using System;

namespace Seed.StageGen
{
    /// <summary>
    /// 地形セルの種別ID。
    /// enum にしないのは、セルの種類が「ゲームごとに増える語彙」だから
    /// （水路・溶岩・橋…をアプリが100以降で発番し、基盤のパス・施工はそのまま使える）。
    /// </summary>
    public readonly struct CellType : IEquatable<CellType>
    {
        /// <summary>未定義（施工されない）。</summary>
        public static readonly CellType None = new CellType(0);

        /// <summary>床（歩行可能）。</summary>
        public static readonly CellType Floor = new CellType(1);

        /// <summary>壁。</summary>
        public static readonly CellType Wall = new CellType(2);

        /// <summary>道路（歩行可能）。</summary>
        public static readonly CellType Road = new CellType(3);

        /// <summary>建物（进入不可のブロック）。</summary>
        public static readonly CellType Building = new CellType(4);

        /// <summary>出入口（歩行可能。建物・部屋の開口部）。</summary>
        public static readonly CellType Door = new CellType(5);

        /// <summary>ID値（アプリ独自は100以降を推奨）。</summary>
        public readonly int Value;

        /// <summary>CellType を生成する。</summary>
        public CellType(int value)
        {
            Value = value;
        }

        /// <summary>
        /// 歩行可能か（標準セルの規約: Floor/Road/Door）。
        /// アプリ独自セル（100+）は「奇数=歩行可能」の規約で拡張できる
        /// （例: 101=浅瀬(可)・102=深い水路(不可)）。
        /// </summary>
        public bool IsWalkable =>
            Value == Floor.Value || Value == Road.Value || Value == Door.Value
            || (Value >= 100 && (Value % 2) == 1);

        /// <summary>同一種別かを返す。</summary>
        public bool Equals(CellType other)
        {
            return Value == other.Value;
        }

        /// <summary>同一種別かを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is CellType other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Cell#{Value}";
        }
    }
}
