namespace Seed.Character
{
    /// <summary>
    /// 被弾のけぞり。攻撃・ガードを割り込み、のけぞり時間の満了で次へ譲る。
    /// 満了時にガード入力が続いていれば構えへ戻る（IntentProposal のガード優先が担う）。
    ///
    /// AllowsRefresh=true —— 多段ヒットでのけぞり中に再度のけぞり命令が来たら
    /// 「もう一度頭から」やり直す（連続被弾の表現）。
    ///
    /// 「のけぞる**べきか**」（スーパーアーマー等の例外）はアプリの方針層が決め、
    /// ここは命令されたのけぞりを演じるだけ。
    /// </summary>
    public sealed class HitBehavior : TimedBehaviorBase
    {
        /// <summary>HitBehavior を生成する。</summary>
        public HitBehavior(float staggerSeconds) : base(staggerSeconds)
        {
        }

        /// <summary>この行動のキー。</summary>
        public override BehaviorKey Key => BehaviorKey.Hit;

        /// <summary>優先度（攻撃を割り込む）。</summary>
        public override int Priority => BehaviorPriorities.Hit;

        /// <summary>多段ヒットののけぞりリフレッシュを許す。</summary>
        public override bool AllowsRefresh => true;
    }
}
