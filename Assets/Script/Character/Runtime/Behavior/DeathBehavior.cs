namespace Seed.Character
{
    /// <summary>
    /// 戦闘不能（終端状態）。
    /// 決して完了せず、何も提案せず、最高優先度なので force 以外では抜けられない。
    /// 生死の真実はロジック側にあり、ここへは死亡通知（Reaction）の force 遷移で入る。
    /// </summary>
    public sealed class DeathBehavior : CharacterBehaviorBase
    {
        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Death;

        /// <summary>優先度（最強。何にも割り込まれない）。</summary>
        public override int Priority => BehaviorPriorities.Death;

        /// <summary>決して完了しない（終端）。</summary>
        public override bool IsCompleted => false;

        /// <summary>いつでも入れる（死亡はどの状態からでも起こる）。</summary>
        public override bool CanEnter(BehaviorContext context)
        {
            return true;
        }

        /// <summary>倒れ状態にする。</summary>
        public override void Enter(BehaviorContext context)
        {
            context.Pose.PlanarSpeed = 0f;
            context.Avatar.SetLocomotionSpeed(0f);
        }

        /// <summary>何も提案しない（終端。ここを抜けるのは Actor 切替の force のみ）。</summary>
        public override BehaviorKey DesiredTransition(BehaviorContext context)
        {
            return BehaviorKey.None;
        }
    }
}
