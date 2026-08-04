using System;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Clock
{
    /// <summary>
    /// ゲーム時間の供給源（時間基盤の本体・純C#）。
    ///
    /// 「状態は Tick、艶は Update」の規約でゲーム進行は全て dt 駆動になっているため、
    /// この1箇所で dt を加工すれば、ポーズ・スローモーション・倍速・ヒットストップが
    /// 全基盤（キャラ・フロー・再生キュー）へ同時に効く——それが時間基盤を分ける理由。
    ///
    /// 使い方: 永続ルートが所有し、毎フレーム最初に Tick(実dt) を呼ぶ。
    /// ゲーム進行は ScaledDelta を、ポーズ中も動くUIは UnscaledDelta を使う。
    /// 操作は命令（SetPausedCommand / SetTimeScaleCommand / HitStopCommand）で行い、
    /// 本クラスが唯一の処理者（SubscribeCommand で強制）。
    /// 注意: GameCore のロジック時間（AdvanceTime）はここを通った ScaledDelta 由来の
    /// ミリ秒で進むため、ポーズはリプレイ上「時間が進まなかった」として自然に記録される。
    /// </summary>
    public sealed class GameClock : IGameClock, IDisposable
    {
        /// <summary>保持中の購読。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(3);

        /// <summary>窓口の返却先。</summary>
        private ServiceRegistry _services;

        /// <summary>ヒットストップの残り実時間（秒）。</summary>
        private float _hitStopRemaining;

        /// <summary>時間倍率（1=等速）。</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>ポーズ中か。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>今フレームのゲーム進行dt（秒）。</summary>
        public float ScaledDelta { get; private set; }

        /// <summary>今フレームの実時間dt（秒）。</summary>
        public float UnscaledDelta { get; private set; }

        /// <summary>Hubへ購読を張り、IGameClock を貸し出す。</summary>
        public void Initialize(MessageHub hub, ServiceRegistry services)
        {
            _services = services;
            services.Register<IGameClock>(this);
            hub.SubscribeCommand<SetPausedCommand>(c => IsPaused = c.IsPaused).AddTo(_subscriptions);
            hub.SubscribeCommand<SetTimeScaleCommand>(OnSetTimeScale).AddTo(_subscriptions);
            hub.SubscribeCommand<HitStopCommand>(OnHitStop).AddTo(_subscriptions);
        }

        /// <summary>毎フレーム最初に呼ぶ（実dt→ゲームdt の加工）。</summary>
        public void Tick(float rawDeltaSeconds)
        {
            UnscaledDelta = rawDeltaSeconds;

            if (IsPaused)
            {
                ScaledDelta = 0f;
                return; // ポーズ中はヒットストップも凍結する（再開後に消化）
            }
            if (_hitStopRemaining > 0f)
            {
                _hitStopRemaining -= rawDeltaSeconds;
                ScaledDelta = 0f;
                return;
            }
            ScaledDelta = rawDeltaSeconds * Scale;
        }

        /// <summary>購読と窓口を片付ける。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
            _services?.Unregister<IGameClock>();
        }

        /// <summary>倍率変更（負値は構成ミスとして0に丸めず例外にする）。</summary>
        private void OnSetTimeScale(SetTimeScaleCommand command)
        {
            if (command.Scale < 0f)
            {
                throw new HubException($"時間倍率に負値が指定された: {command.Scale}");
            }
            Scale = command.Scale;
        }

        /// <summary>ヒットストップ（重なったら長い方が残る。加算はしない）。</summary>
        private void OnHitStop(HitStopCommand command)
        {
            if (command.Seconds > _hitStopRemaining)
            {
                _hitStopRemaining = command.Seconds;
            }
        }
    }
}
