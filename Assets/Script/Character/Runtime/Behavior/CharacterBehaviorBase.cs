namespace Seed.Character
{
    /// <summary>
    /// 行動の実装基底（新しい行動を足すときの出発点）。
    ///
    /// 「行動の数だけクラスを用意する」設計なので、増やすコストが小さいことが最重要。
    /// 本基底は妥当な既定（最弱優先度・常に完了・生存中のみ入れる・意図どおりに次を提案）を
    /// 与えるため、独自行動は Key と必要な差分だけを書けばよい。
    /// ICharacterBehavior へメンバーが増えても、既定実装がここに入るので既存の行動は壊れない。
    /// </summary>
    public abstract class CharacterBehaviorBase : ICharacterBehavior
    {
        /// <summary>この行動のキー（実装が必ず与える）。</summary>
        public abstract BehaviorKey Key { get; }

        /// <summary>割り込み強度（既定は最弱＝誰にでも譲る）。</summary>
        public virtual int Priority => BehaviorPriorities.Locomotion;

        /// <summary>完了済みか（既定は常に完了＝拘束しない行動）。</summary>
        public virtual bool IsCompleted => true;

        /// <summary>同一キーへの再入を許すか（既定は禁止）。</summary>
        public virtual bool AllowsRefresh => false;

        /// <summary>この行動へ入れるか（既定は生存中のみ）。</summary>
        public virtual bool CanEnter(BehaviorContext context)
        {
            return context.IsAlive;
        }

        /// <summary>行動開始（既定は何もしない）。</summary>
        public virtual void Enter(BehaviorContext context)
        {
        }

        /// <summary>行動を1Tick進める（既定は何もしない）。</summary>
        public virtual void Tick(BehaviorContext context, float deltaTime)
        {
        }

        /// <summary>行動終了（既定は何もしない）。</summary>
        public virtual void Exit(BehaviorContext context)
        {
        }

        /// <summary>
        /// 次に行きたい行動の提案（既定は意図の共通規則）。
        /// 完了していない間は現状維持、完了していれば意図どおり（意図なしなら待機）。
        /// </summary>
        public virtual BehaviorKey DesiredTransition(BehaviorContext context)
        {
            if (!IsCompleted)
            {
                return BehaviorKey.None;
            }
            var proposal = IntentProposal.Next(context);
            if (proposal.Equals(BehaviorKey.None))
            {
                return Key.Equals(BehaviorKey.Idle) ? BehaviorKey.None : BehaviorKey.Idle;
            }
            return proposal.Equals(Key) ? BehaviorKey.None : proposal;
        }

        /// <summary>アニメーションイベントを受ける（既定は無視）。</summary>
        public virtual void OnAvatarEvent(BehaviorContext context, int eventId)
        {
        }

        /// <summary>内部状態を初期化する（既定は何もしない）。</summary>
        public virtual void Reset()
        {
        }
    }
}
