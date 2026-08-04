namespace Seed.Character
{
    /// <summary>
    /// アニメーションイベントの標準ID（Avatar→行動へ戻す合図）。
    ///
    /// アニメクリップ側の AnimationEvent から Avatar が受け取り、
    /// <see cref="IAvatarEventSink.PostAvatarEvent"/> で現在の行動へ配る。
    /// これにより「攻撃の当たり判定はアニメの何フレーム目か」を
    /// 秒数の手合わせではなくアニメ側の真実で駆動できる。
    ///
    /// 予約: 1〜99 は基盤の標準イベント。アプリ独自は 100 以降を推奨
    /// （int を直接使うのは、イベントの種類がゲームごとに増える語彙だから）。
    /// </summary>
    public static class AvatarEventId
    {
        /// <summary>なし。</summary>
        public const int None = 0;

        /// <summary>当たり判定の有効化（攻撃の有効フレーム開始）。</summary>
        public const int HitboxBegin = 1;

        /// <summary>当たり判定の無効化（攻撃の有効フレーム終了）。</summary>
        public const int HitboxEnd = 2;

        /// <summary>次の攻撃へ繋げられる受付窓の開始（コンボ）。</summary>
        public const int ComboWindowBegin = 3;

        /// <summary>コンボ受付窓の終了。</summary>
        public const int ComboWindowEnd = 4;

        /// <summary>行動の演出が最後まで再生された（拘束を解いてよい合図）。</summary>
        public const int MotionFinished = 5;

        /// <summary>足音（SE再生のきっかけ）。</summary>
        public const int Footstep = 6;
    }
}
