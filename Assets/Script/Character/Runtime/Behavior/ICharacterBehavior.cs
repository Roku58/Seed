namespace Seed.Character
{
    /// <summary>
    /// 行動1種の状態機械単位（Idle・Walk・Attack…行動の数だけ実装する）。
    ///
    /// 実装は <see cref="CharacterBehaviorBase"/>（または時間で終わる行動なら
    /// <see cref="TimedBehaviorBase"/>）を継承するのが既定の作法
    /// ——本インターフェースへメンバーを足しても既存の行動が壊れないようにするため、
    /// 直接実装ではなく基底クラス経由を推奨する。
    ///
    /// 遷移の裁定は CharacterActor が一元的に行う:
    /// - 現在行動の実効優先度 = IsCompleted ? int.MinValue : Priority
    /// - 次行動の Priority がそれ以上 かつ CanEnter が真なら遷移（Exit→Enter）
    /// - force 指定（死亡・Actor切替リセット）はすべて無視して遷移
    /// この規則により「攻撃中はガード不可」「被弾は攻撃を割り込む」
    /// 「死亡は何にも割り込まれない」が優先度の大小だけで自然に成立する。
    /// </summary>
    public interface ICharacterBehavior
    {
        /// <summary>この行動のキー。</summary>
        BehaviorKey Key { get; }

        /// <summary>割り込み強度（BehaviorPriorities 参照）。</summary>
        int Priority { get; }

        /// <summary>完了済みか。完了後はどの行動でも遷移してよい。</summary>
        bool IsCompleted { get; }

        /// <summary>
        /// 同一キーへの再入（入り直し）を許すか。
        /// 多段ヒットのけぞりのように「同じ行動をもう一度頭から」が要る行動だけ true にする
        /// （既定 false。連打で攻撃がキャンセルされ続ける事故を防ぐため）。
        /// </summary>
        bool AllowsRefresh { get; }

        /// <summary>この行動へ入れるか（遷移可否の最終拒否権）。</summary>
        bool CanEnter(BehaviorContext context);

        /// <summary>行動開始（演出のトリガーは Avatar.OnBehaviorChanged が担うため、ここは内部状態のリセットが主）。</summary>
        void Enter(BehaviorContext context);

        /// <summary>行動を1Tick進める（移動・時間経過はここ）。</summary>
        void Tick(BehaviorContext context, float deltaTime);

        /// <summary>行動終了（構え解除など、次の行動に残してはいけない状態の後始末）。</summary>
        void Exit(BehaviorContext context);

        /// <summary>次に行きたい行動の提案（None なら維持）。裁定は Actor が行う。</summary>
        BehaviorKey DesiredTransition(BehaviorContext context);

        /// <summary>
        /// Avatar からのアニメーションイベントを受ける（当たり判定フレーム等）。
        /// 現在の行動にだけ配られる。
        /// </summary>
        void OnAvatarEvent(BehaviorContext context, int eventId);

        /// <summary>プールから再利用する前に内部状態を初期化する。</summary>
        void Reset();
    }
}
