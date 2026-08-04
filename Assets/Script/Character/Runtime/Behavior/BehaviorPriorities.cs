namespace Seed.Character
{
    /// <summary>
    /// 標準行動の優先度（割り込み強度）の既定値。
    /// 値の間を空けてあるのは、アプリ独自の行動を間に差し込む余地のため
    /// （例: 回避=25 なら「攻撃は割り込めるが被弾には割り込まれる」）。
    /// </summary>
    public static class BehaviorPriorities
    {
        /// <summary>待機・移動（最弱。誰にでも譲る）。</summary>
        public const int Locomotion = 0;

        /// <summary>ガード。</summary>
        public const int Guard = 10;

        /// <summary>攻撃。</summary>
        public const int Attack = 20;

        /// <summary>被弾のけぞり（攻撃を割り込む）。</summary>
        public const int Hit = 30;

        /// <summary>戦闘不能（何にも割り込まれない）。</summary>
        public const int Death = 100;
    }
}
