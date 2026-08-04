using System;

namespace Seed.Motion
{
    /// <summary>モーション1本の再生仕様（純データ。クリップ実体は持たない＝テスト可能）。</summary>
    public readonly struct MotionDescriptor
    {
        /// <summary>長さ（秒）。</summary>
        public readonly float DurationSeconds;

        /// <summary>ループするか。</summary>
        public readonly bool Loop;

        /// <summary>正規化時間イベント（‰位置 → AvatarEventId 値。昇順で登録すること）。</summary>
        public readonly MotionEvent[] Events;

        /// <summary>MotionDescriptor を生成する。</summary>
        public MotionDescriptor(float durationSeconds, bool loop, MotionEvent[] events = null)
        {
            DurationSeconds = durationSeconds <= 0f ? 0.001f : durationSeconds;
            Loop = loop;
            Events = events ?? Array.Empty<MotionEvent>();
        }
    }

    /// <summary>正規化時間イベント1件（クリップを編集せずに当たり判定窓などを定義できる）。</summary>
    public readonly struct MotionEvent
    {
        /// <summary>発火位置（クリップの正規化時間‰。0〜1000）。</summary>
        public readonly int Permille;

        /// <summary>発火するイベントID（AvatarEventId の値体系）。</summary>
        public readonly int EventId;

        /// <summary>MotionEvent を生成する。</summary>
        public MotionEvent(int permille, int eventId)
        {
            Permille = permille;
            EventId = eventId;
        }
    }

    /// <summary>
    /// クロスフェード再生の状態機械（純C#。Playables への重み適用はバックエンドが行う）。
    ///
    /// - Play で新モーションへ切り替え、フェード秒かけて重みを 0→1 に運ぶ
    ///   （前のモーションは 1→0。重みの合計は常に1）
    /// - 正規化時間‰の通過でイベントを発火する（ループ折り返しでは
    ///   末尾側→先頭側の順に発火し、取りこぼさない）
    /// 純C#なのでフェード・イベント発火のタイミングを EditMode テストで検証できる。
    /// </summary>
    public sealed class CrossfadeState
    {
        /// <summary>現在のモーション仕様。</summary>
        private MotionDescriptor _current;

        /// <summary>現在モーションの経過秒。</summary>
        private float _time;

        /// <summary>フェードの残り秒。</summary>
        private float _fadeRemaining;

        /// <summary>フェードの全長秒。</summary>
        private float _fadeDuration;

        /// <summary>直前Tickの正規化‰（イベントの通過検出用）。</summary>
        private int _lastPermille;

        /// <summary>再生中のモーションがあるか。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>現在モーションの重み（0〜1。残りは直前モーションの重み）。</summary>
        public float CurrentWeight { get; private set; } = 1f;

        /// <summary>現在モーションの正規化時間（0〜1。ループ中は折り返し後の値）。</summary>
        public float NormalizedTime =>
            _current.Loop
                ? _time / _current.DurationSeconds % 1f
                : Math.Min(_time / _current.DurationSeconds, 1f);

        /// <summary>非ループモーションを最後まで再生し切ったか。</summary>
        public bool IsFinished => !_current.Loop && _time >= _current.DurationSeconds;

        /// <summary>新しいモーションへ切り替える（fadeSeconds=0 で即時）。</summary>
        public void Play(in MotionDescriptor descriptor, float fadeSeconds)
        {
            _current = descriptor;
            _time = 0f;
            _lastPermille = 0;
            _fadeDuration = fadeSeconds > 0f ? fadeSeconds : 0f;
            _fadeRemaining = _fadeDuration;
            CurrentWeight = _fadeDuration > 0f ? 0f : 1f;
            IsPlaying = true;
        }

        /// <summary>
        /// 時間を進め、フェード重みを更新し、通過した正規化イベントを発火する。
        /// onEvent には MotionEvent.EventId が渡る（AvatarEventId の値体系）。
        /// </summary>
        public void Tick(float deltaTime, Action<int> onEvent = null)
        {
            if (!IsPlaying)
            {
                return;
            }

            // フェード（線形。重みの適用はバックエンドの仕事）
            if (_fadeRemaining > 0f)
            {
                _fadeRemaining -= deltaTime;
                CurrentWeight = _fadeRemaining <= 0f
                    ? 1f
                    : 1f - _fadeRemaining / _fadeDuration;
            }

            var previousTime = _time;
            _time += deltaTime;

            if (onEvent == null || _current.Events.Length == 0)
            {
                _lastPermille = ToPermille(_time);
                return;
            }

            // 正規化‰の通過検出（ループは 末尾→折り返し→先頭 の順で漏らさない）
            var fromPermille = _lastPermille;
            var toPermille = ToPermille(_time);
            if (_current.Loop && WrappedThisTick(previousTime))
            {
                FireRange(fromPermille, 1000, onEvent);
                FireRange(-1, toPermille, onEvent); // 0‰ ちょうどのイベントも拾う
            }
            else
            {
                FireRange(fromPermille, toPermille, onEvent);
            }
            _lastPermille = toPermille;
        }

        /// <summary>経過秒→正規化‰（ループは折り返し後）。</summary>
        private int ToPermille(float time)
        {
            var normalized = _current.Loop
                ? time / _current.DurationSeconds % 1f
                : Math.Min(time / _current.DurationSeconds, 1f);
            return (int)(normalized * 1000f);
        }

        /// <summary>このTickでループ境界をまたいだか。</summary>
        private bool WrappedThisTick(float previousTime)
        {
            return (int)(previousTime / _current.DurationSeconds)
                != (int)(_time / _current.DurationSeconds);
        }

        /// <summary>(from, to] に入るイベントを登録順に発火する。</summary>
        private void FireRange(int fromExclusive, int toInclusive, Action<int> onEvent)
        {
            var events = _current.Events;
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].Permille > fromExclusive && events[i].Permille <= toInclusive)
                {
                    onEvent(events[i].EventId);
                }
            }
        }
    }
}
