namespace Seed.Character
{
    /// <summary>
    /// 意図（CharacterIntent）から「次に行きたい行動」を導く共通規則。
    /// Idle・Locomotion・完了した行動が同じ規則で次を提案することで、
    /// 「被弾直後にガードへ戻る」「攻撃直後に移動へ移る」が1フレームの空白なく繋がる。
    ///
    /// 優先順位: ガード継続 &gt; 離散行動の要求 &gt; 移動 &gt; なし。
    /// ガードを行動要求より先に見るのは「ガード中は攻撃を出さない」仕様のため
    /// （ガード構え中の攻撃キーは無視される）。
    ///
    /// アプリ独自の行動基底からも使えるよう public にしてある
    /// （独自行動が「意図どおりに次へ譲る」既定挙動を再実装しないで済む）。
    /// </summary>
    public static class IntentProposal
    {
        /// <summary>移動意図とみなす最小の二乗長。</summary>
        private const float MoveEpsilon = 0.0001f;

        /// <summary>意図から次の行動を提案する（None なら現状維持でよい）。</summary>
        public static BehaviorKey Next(BehaviorContext context)
        {
            if (context.Intent.GuardHeld)
            {
                return BehaviorKey.Guard;
            }
            if (!context.Intent.RequestedAction.Equals(BehaviorKey.None))
            {
                return context.Intent.RequestedAction;
            }
            if (context.Intent.MoveDirection.sqrMagnitude > MoveEpsilon)
            {
                return BehaviorKey.Locomotion;
            }
            return BehaviorKey.None;
        }
    }
}
