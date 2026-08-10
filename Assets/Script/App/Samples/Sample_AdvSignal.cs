using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ADVの台本からコード側へ飛ばす合図（通知）。
    ///
    /// 演技 Signal（AdvActKind.Signal）で発火し、Hub を購読している側
    /// （方針クラス・任意の基盤・デバッグ）が自由に処理する——
    /// 報酬付与・フラグ更新・実績解除など「台本→ゲーム状態」への唯一の口。
    /// 意味づけは Key の文字列に対して購読側が行う（台本は合図を出すだけ）。
    /// </summary>
    public readonly struct Sample_AdvSignal : INotificationMessage
    {
        /// <summary>発火元のイベントID。</summary>
        public readonly int EventId;

        /// <summary>合図の名前（意味づけは購読側）。</summary>
        public readonly string Key;

        /// <summary>おまけの整数（対象キャラ・個数など。演技の ActorId がそのまま入る）。</summary>
        public readonly int ActorId;

        /// <summary>おまけの数値（演技の X がそのまま入る）。</summary>
        public readonly float Value;

        /// <summary>Sample_AdvSignal を生成する。</summary>
        public Sample_AdvSignal(int eventId, string key, int actorId, float value)
        {
            EventId = eventId;
            Key = key;
            ActorId = actorId;
            Value = value;
        }
    }
}
