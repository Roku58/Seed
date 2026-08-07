namespace Seed.UI
{
    /// <summary>
    /// 長押し判定（純C#）。
    ///
    /// 「押し始めから一定時間の保持で1回だけ成立」という判定だけを切り出してある。
    /// MonoBehaviour の Update から時刻を渡して使う形にしたのは、
    /// 実時間に依存する判定を EditMode テストで1秒待たずに検証するため。
    /// </summary>
    public sealed class LongPressTracker
    {
        /// <summary>押している最中か。</summary>
        private bool _holding;

        /// <summary>押し始めの時刻。</summary>
        private float _startTime;

        /// <summary>今回の保持で成立済みか（1回の押下で1回だけ発火させる）。</summary>
        public bool Fired { get; private set; }

        /// <summary>押し始めを記録する。</summary>
        public void Begin(float now)
        {
            _holding = true;
            _startTime = now;
            Fired = false;
        }

        /// <summary>押下を終了する（成立済みフラグは次の Begin まで残す＝クリック抑止に使う）。</summary>
        public void Cancel()
        {
            _holding = false;
        }

        /// <summary>成立済みフラグも含めて初期状態へ戻す。</summary>
        public void Reset()
        {
            _holding = false;
            Fired = false;
        }

        /// <summary>
        /// 成立判定（毎フレーム呼ぶ）。閾値に達した瞬間に一度だけ true を返す。
        /// thresholdSeconds が 0 以下なら長押し機能は無効。
        /// </summary>
        public bool TryFire(float now, float thresholdSeconds)
        {
            if (!_holding || Fired || thresholdSeconds <= 0f)
            {
                return false;
            }
            if (now - _startTime < thresholdSeconds)
            {
                return false;
            }
            Fired = true;
            return true;
        }
    }
}
