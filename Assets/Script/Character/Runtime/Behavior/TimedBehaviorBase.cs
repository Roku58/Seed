namespace Seed.Character
{
    /// <summary>
    /// 「一定時間キャラを拘束して終わる」行動の基底（攻撃・のけぞり・回避・詠唱など）。
    ///
    /// この形の行動が最も多いため基底に持たせた。新しい必殺技を追加したいときは
    /// Key と持続秒を与えるだけの数行のクラスで済む——「内容を増やしやすい」ことを優先した設計。
    ///
    /// 演出（アニメクリップ）側で終わりを決めたい場合は
    /// <see cref="AvatarEventId.MotionFinished"/> を受け取った時点で完了扱いになるため、
    /// 秒数の手合わせに頼らずアニメの真実で拘束を解ける。
    /// </summary>
    public abstract class TimedBehaviorBase : CharacterBehaviorBase
    {
        /// <summary>拘束の持続秒（0以下なら即完了）。</summary>
        private readonly float _durationSeconds;

        /// <summary>経過秒。</summary>
        private float _elapsed;

        /// <summary>演出側から完了を告げられたか。</summary>
        private bool _finishedByAvatar;

        /// <summary>TimedBehaviorBase を生成する。</summary>
        protected TimedBehaviorBase(float durationSeconds)
        {
            _durationSeconds = durationSeconds;
        }

        /// <summary>経過秒（派生が演出の進捗に使ってよい）。</summary>
        protected float Elapsed => _elapsed;

        /// <summary>拘束の持続秒。</summary>
        protected float DurationSeconds => _durationSeconds;

        /// <summary>正規化した進捗（0〜1）。アニメ側との同期に使う。</summary>
        public float NormalizedTime => _durationSeconds <= 0f
            ? 1f
            : (_elapsed / _durationSeconds > 1f ? 1f : _elapsed / _durationSeconds);

        /// <summary>時間を使い切ったか、演出側から完了を告げられたか。</summary>
        public override bool IsCompleted => _finishedByAvatar || _elapsed >= _durationSeconds;

        /// <summary>経過をリセットして拘束を始める。</summary>
        public override void Enter(BehaviorContext context)
        {
            _elapsed = 0f;
            _finishedByAvatar = false;
        }

        /// <summary>時間を進める。</summary>
        public override void Tick(BehaviorContext context, float deltaTime)
        {
            _elapsed += deltaTime;
        }

        /// <summary>演出の終了通知で拘束を解く（アニメ長を真実にする経路）。</summary>
        public override void OnAvatarEvent(BehaviorContext context, int eventId)
        {
            if (eventId == AvatarEventId.MotionFinished)
            {
                _finishedByAvatar = true;
            }
        }

        /// <summary>プール再利用のために経過を初期化する。</summary>
        public override void Reset()
        {
            _elapsed = 0f;
            _finishedByAvatar = false;
        }
    }
}
