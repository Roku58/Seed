using System;

namespace Seed.StageGen
{
    /// <summary>
    /// 配置物の種別ID（プレイヤー起点・敵・出口・ショップ・宝箱・イベント…）。
    /// 「何を置くか」はゲームごとに増える語彙なので int 値型
    /// （アプリ独自は100以降を発番し、施工側の配置表に1行足すだけで増える）。
    /// </summary>
    public readonly struct PlacementKind : IEquatable<PlacementKind>
    {
        /// <summary>なし。</summary>
        public static readonly PlacementKind None = new PlacementKind(0);

        /// <summary>プレイヤーの開始地点。</summary>
        public static readonly PlacementKind PlayerSpawn = new PlacementKind(1);

        /// <summary>敵の出現地点（RefId=敵定義ID）。</summary>
        public static readonly PlacementKind EnemySpawn = new PlacementKind(2);

        /// <summary>出口（踏むと退出）。</summary>
        public static readonly PlacementKind Exit = new PlacementKind(3);

        /// <summary>ショップ（踏むと店へ）。</summary>
        public static readonly PlacementKind Shop = new PlacementKind(4);

        /// <summary>宝箱（RefId=アイテム定義ID）。</summary>
        public static readonly PlacementKind Chest = new PlacementKind(5);

        /// <summary>汎用イベントトリガー（RefId=イベントID）。</summary>
        public static readonly PlacementKind EventTrigger = new PlacementKind(6);

        /// <summary>ID値（アプリ独自は100以降を推奨）。</summary>
        public readonly int Value;

        /// <summary>PlacementKind を生成する。</summary>
        public PlacementKind(int value)
        {
            Value = value;
        }

        /// <summary>同一種別かを返す。</summary>
        public bool Equals(PlacementKind other)
        {
            return Value == other.Value;
        }

        /// <summary>同一種別かを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is PlacementKind other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Placement#{Value}";
        }
    }

    /// <summary>
    /// 配置物1件（設計図に載る純データ）。
    /// RefId はマスターデータのID（敵定義・アイテム定義・イベント定義…意味はアプリが決める）。
    /// </summary>
    public readonly struct Placement
    {
        /// <summary>種別。</summary>
        public readonly PlacementKind Kind;

        /// <summary>参照ID（マスターデータの値体系。0=参照なし）。</summary>
        public readonly int RefId;

        /// <summary>グリッドX。</summary>
        public readonly int X;

        /// <summary>グリッドY。</summary>
        public readonly int Y;

        /// <summary>Placement を生成する。</summary>
        public Placement(PlacementKind kind, int refId, int x, int y)
        {
            Kind = kind;
            RefId = refId;
            X = x;
            Y = y;
        }
    }
}
