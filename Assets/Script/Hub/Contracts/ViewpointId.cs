using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 境界を越えて「視点（カメラ）」を指すための共有ID。
    ///
    /// カメラの実体（Cinemachine のバーチャルカメラ）はカメラ基盤の内側にあり、
    /// 他の基盤はこのIDしか知らない——UI やイベントが「一人称に切り替えて」と
    /// 頼むだけで、カメラの作り（追従方式・レンズ・ブレンド）に触らずに済む。
    ///
    /// 1〜99 は基盤の予約帯、100 以降がアプリ独自（ScreenId と同じ流儀）。
    /// </summary>
    public readonly struct ViewpointId : IEquatable<ViewpointId>
    {
        /// <summary>「視点なし」を表す予約値。</summary>
        public static readonly ViewpointId None = new ViewpointId(0);

        /// <summary>一人称（FPS）。</summary>
        public static readonly ViewpointId FirstPerson = new ViewpointId(1);

        /// <summary>三人称（TPS・肩越し）。</summary>
        public static readonly ViewpointId ThirdPerson = new ViewpointId(2);

        /// <summary>俯瞰（見下ろし・マップ確認）。</summary>
        public static readonly ViewpointId Overhead = new ViewpointId(3);

        /// <summary>演出用（カットシーン・決着の寄り）。</summary>
        public static readonly ViewpointId Cutscene = new ViewpointId(4);

        /// <summary>ID値（1以上が有効）。</summary>
        public readonly int Value;

        /// <summary>ViewpointId を生成する。</summary>
        public ViewpointId(int value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(ViewpointId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is ViewpointId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Viewpoint#{Value}";
        }
    }
}
