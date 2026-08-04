namespace Seed.Character
{
    /// <summary>
    /// 待機。常に完了済み＝誰にでも席を譲る、状態機械の休符。
    /// 意図が生まれたら IntentProposal の規則で次の行動を提案する（基底の既定挙動）。
    /// </summary>
    public sealed class IdleBehavior : CharacterBehaviorBase
    {
        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Idle;

        /// <summary>いつでも入れる（死亡直後の待機遷移も許す）。</summary>
        public override bool CanEnter(BehaviorContext context)
        {
            return true;
        }

        /// <summary>停止状態にする。</summary>
        public override void Enter(BehaviorContext context)
        {
            context.Pose.PlanarSpeed = 0f;
            context.Avatar.SetLocomotionSpeed(0f);
        }
    }
}
