namespace Seed.Character
{
    /// <summary>
    /// ガード（構え続ける継続系）。
    /// 意図の GuardHeld が続く限り占有し、離されたら完了して次へ譲る。
    /// ガード中の攻撃キーは IntentProposal の規則（ガード優先）で無視される。
    /// ダメージ軽減の真実はロジック側の仕事——ここは構えの見た目と行動拘束のみ。
    /// </summary>
    public sealed class GuardBehavior : CharacterBehaviorBase
    {
        /// <summary>直近の意図でガードが押されていたか。</summary>
        private bool _held;

        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Guard;

        /// <summary>優先度（移動より強く、攻撃より弱い）。</summary>
        public override int Priority => BehaviorPriorities.Guard;

        /// <summary>ガードが離されたら完了。</summary>
        public override bool IsCompleted => !_held;

        /// <summary>構えを始める。</summary>
        public override void Enter(BehaviorContext context)
        {
            _held = true;
        }

        /// <summary>意図から構え継続を読み取る。</summary>
        public override void Tick(BehaviorContext context, float deltaTime)
        {
            _held = context.Intent.GuardHeld;
        }

        /// <summary>構えを解く。</summary>
        public override void Exit(BehaviorContext context)
        {
            _held = false;
        }

        /// <summary>プール再利用のために状態を初期化する。</summary>
        public override void Reset()
        {
            _held = false;
        }
    }
}
