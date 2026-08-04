namespace Seed.Character
{
    /// <summary>
    /// 攻撃。振りの持続時間だけこの行動を占有し、満了で完了する。
    /// 未完了中は同キー再入不可（AllowsRefresh=false）なので連打は自然に拒否される。
    ///
    /// 「攻撃が実際に当たったか・通ったか」の真実はロジック側（GameCore）の仕事。
    /// ここは見た目上の拘束時間と、当たり判定の有効フレーム（アニメイベント由来）だけを持つ。
    /// 攻撃開始の外部通知は Actor の Transitioned イベント（→ Agent.BehaviorStarted）が担う。
    ///
    /// ※既知の割り切り: ロジック側の発動判定（スタミナ等）に却下されても振り演出は出る。
    ///   厳密に一致させたい場合は契約に AttackResolved を追加して巻き戻す。
    /// </summary>
    public sealed class AttackBehavior : TimedBehaviorBase
    {
        /// <summary>攻撃判定が有効な区間にいるか（アニメイベントで開閉する）。</summary>
        public bool IsHitboxActive { get; private set; }

        /// <summary>次の攻撃へ繋げられる受付窓にいるか（コンボ）。</summary>
        public bool IsComboWindowOpen { get; private set; }

        /// <summary>AttackBehavior を生成する。</summary>
        public AttackBehavior(float durationSeconds) : base(durationSeconds)
        {
        }

        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Attack;

        /// <summary>優先度（ガードより強く、被弾より弱い）。</summary>
        public override int Priority => BehaviorPriorities.Attack;

        /// <summary>振り始めに判定と受付窓を閉じる。</summary>
        public override void Enter(BehaviorContext context)
        {
            base.Enter(context);
            IsHitboxActive = false;
            IsComboWindowOpen = false;
        }

        /// <summary>振り終わりに判定と受付窓を確実に閉じる（開いたまま次へ持ち越さない）。</summary>
        public override void Exit(BehaviorContext context)
        {
            IsHitboxActive = false;
            IsComboWindowOpen = false;
        }

        /// <summary>アニメの当たり判定フレーム・コンボ窓・演出終了を受け取る。</summary>
        public override void OnAvatarEvent(BehaviorContext context, int eventId)
        {
            base.OnAvatarEvent(context, eventId);
            switch (eventId)
            {
                case AvatarEventId.HitboxBegin:
                    IsHitboxActive = true;
                    break;
                case AvatarEventId.HitboxEnd:
                    IsHitboxActive = false;
                    break;
                case AvatarEventId.ComboWindowBegin:
                    IsComboWindowOpen = true;
                    break;
                case AvatarEventId.ComboWindowEnd:
                    IsComboWindowOpen = false;
                    break;
            }
        }

        /// <summary>プール再利用のために状態を初期化する。</summary>
        public override void Reset()
        {
            base.Reset();
            IsHitboxActive = false;
            IsComboWindowOpen = false;
        }
    }
}
