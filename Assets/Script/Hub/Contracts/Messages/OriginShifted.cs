namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 世界の原点がずらされたという通知（過去形）。
    ///
    /// 広いフィールドを進み続けると座標の絶対値が大きくなり、float の有効桁が足りなくなって
    /// 位置がガタつく・当たり判定が不安定になる。それを避けるため、一定距離ごとに
    /// 「世界全体を原点へ引き戻す」のが原点回帰である。
    ///
    /// 座標を自前で保持している基盤・アプリはこれを購読して自分の値へ Delta を加える
    /// （Transform を持つものは親をまとめて動かせば済むが、純C#側に持っている座標——
    /// 追跡中の目標地点・トリガー位置・経路点——は自分で追従する必要がある）。
    /// </summary>
    public readonly struct OriginShifted : INotificationMessage
    {
        /// <summary>今回ずらした量（自分の保持座標にこれを加えると追従できる）。</summary>
        public readonly HubVector3 Delta;

        /// <summary>累積のずらし量（見た目の座標 − これ ＝ 開始時からの絶対座標）。</summary>
        public readonly HubVector3 TotalOffset;

        /// <summary>OriginShifted を生成する。</summary>
        public OriginShifted(HubVector3 delta, HubVector3 totalOffset)
        {
            Delta = delta;
            TotalOffset = totalOffset;
        }
    }
}
