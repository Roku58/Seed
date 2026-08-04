namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】ターン終了イベント。ターン限定効果（まもる等）が自己解除するために使う。</summary>
    public sealed class Sample_TurnEndEvent : LogicEvent
    {
        /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
        public override void Reset()
        {
        }
    }
}
