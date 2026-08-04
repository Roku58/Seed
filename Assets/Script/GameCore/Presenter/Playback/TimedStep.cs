using System;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// 「開始時に何かして、一定時間待ち、完了時に何かする」だけの最小ステップ実装。
    /// ログ表示・単純なUI点滅などはこれで足りる。凝った演出は IPlaybackStep を自作する。
    ///
    /// スキップ（Complete）では未開始でも onStart→onComplete の順で必ず両方を走らせる。
    /// 「開始で入り、完了で戻す」対称な演出（ガードポーズ等）が片側だけ適用されて
    /// 画面に residue が残るのを防ぐため。
    /// </summary>
    public sealed class TimedStep : IPlaybackStep
    {
        /// <summary>開始時の処理。</summary>
        private readonly Action _onStart;
        /// <summary>完了時の処理。</summary>
        private readonly Action _onComplete;
        /// <summary>残り時間（秒）。負値の分が使い残した時間になる。</summary>
        private float _remaining;
        /// <summary>開始処理を実行済みか。</summary>
        private bool _started;
        /// <summary>完了処理を実行済みか（Tick と Complete の二重呼び出しを防ぐ）。</summary>
        private bool _finished;

        /// <summary>TimedStep を生成する。</summary>
        public TimedStep(float durationSeconds, Action onStart = null, Action onComplete = null)
        {
            _remaining = durationSeconds;
            _onStart = onStart;
            _onComplete = onComplete;
        }

        /// <summary>完了時に使い切らなかった時間（秒）。表示時間を超過した分がそのまま余りになる。</summary>
        public float Remainder => _remaining < 0f ? -_remaining : 0f;

        /// <summary>経過時間を与えて再生を進める。</summary>
        public bool Tick(float deltaSeconds)
        {
            if (_finished)
            {
                return true;
            }

            if (!_started)
            {
                _started = true;
                _onStart?.Invoke();
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            _finished = true;
            _onComplete?.Invoke();
            return true;
        }

        /// <summary>残り時間を捨て、終端状態だけを適用して完了する（未開始なら開始処理も先に走らせる）。</summary>
        public void Complete()
        {
            if (_finished)
            {
                return;
            }

            if (!_started)
            {
                _started = true;
                _onStart?.Invoke();
            }

            _finished = true;
            _remaining = 0f; // スキップでは余り時間を主張しない（残りステップも同様に即完了させるため）
            _onComplete?.Invoke();
        }
    }
}
