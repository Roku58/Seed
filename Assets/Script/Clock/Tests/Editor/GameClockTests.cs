using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Clock.Tests
{
    /// <summary>GameClock（時間基盤）のテスト。すべて純C#。</summary>
    public sealed class GameClockTests
    {
        /// <summary>Hubと窓口つきで時計を組む。</summary>
        private static (GameClock Clock, MessageHub Hub, ServiceRegistry Services) Build()
        {
            var hub = new MessageHub();
            var services = new ServiceRegistry();
            var clock = new GameClock();
            clock.Initialize(hub, services);
            return (clock, hub, services);
        }

        /// <summary>等速では実dtがそのままゲームdtになる。</summary>
        [Test]
        public void DefaultScale_PassesDeltaThrough()
        {
            var (clock, _, _) = Build();
            clock.Tick(0.016f);
            Assert.AreEqual(0.016f, clock.ScaledDelta, 0.0001f);
            Assert.AreEqual(0.016f, clock.UnscaledDelta, 0.0001f);
        }

        /// <summary>倍率命令でゲームdtが伸縮する（実dtは不変）。</summary>
        [Test]
        public void SetTimeScale_ScalesGameDelta()
        {
            var (clock, hub, _) = Build();
            hub.PublishCommand(new SetTimeScaleCommand(0.5f));

            clock.Tick(0.02f);

            Assert.AreEqual(0.01f, clock.ScaledDelta, 0.0001f, "スローモーション");
            Assert.AreEqual(0.02f, clock.UnscaledDelta, 0.0001f, "実時間は不変");
        }

        /// <summary>ポーズ中はゲームdtが0になり、解除で復帰する。</summary>
        [Test]
        public void Pause_ZeroesScaledDelta()
        {
            var (clock, hub, _) = Build();
            hub.PublishCommand(new SetPausedCommand(true));
            clock.Tick(0.016f);
            Assert.AreEqual(0f, clock.ScaledDelta);
            Assert.AreEqual(0.016f, clock.UnscaledDelta, 0.0001f, "UI用の実dtは流れ続ける");

            hub.PublishCommand(new SetPausedCommand(false));
            clock.Tick(0.016f);
            Assert.Greater(clock.ScaledDelta, 0f);
        }

        /// <summary>ヒットストップは指定実時間だけゲームdtを止め、自然に解ける。</summary>
        [Test]
        public void HitStop_FreezesForDuration()
        {
            var (clock, hub, _) = Build();
            hub.PublishCommand(new HitStopCommand(0.05f));

            clock.Tick(0.03f);
            Assert.AreEqual(0f, clock.ScaledDelta, "停止中");
            clock.Tick(0.03f);
            Assert.AreEqual(0f, clock.ScaledDelta, "まだ停止中（残り0.02sを今フレームで消化）");
            clock.Tick(0.03f);
            Assert.Greater(clock.ScaledDelta, 0f, "解けた");
        }

        /// <summary>ヒットストップの重複要求は長い方が残る（加算しない）。</summary>
        [Test]
        public void HitStop_OverlapKeepsLonger()
        {
            var (clock, hub, _) = Build();
            hub.PublishCommand(new HitStopCommand(0.02f));
            hub.PublishCommand(new HitStopCommand(0.05f));
            hub.PublishCommand(new HitStopCommand(0.01f)); // 短い要求は無視される

            clock.Tick(0.04f);
            Assert.AreEqual(0f, clock.ScaledDelta, "長い方（0.05）が生きている");
            clock.Tick(0.04f);
            Assert.AreEqual(0f, clock.ScaledDelta, "残り0.01を今フレームで消化");
            clock.Tick(0.04f);
            Assert.Greater(clock.ScaledDelta, 0f, "解けた（短い要求0.02なら既に解けていたはず）");
        }

        /// <summary>IGameClock として貸し出され、Dispose で返却される。負の倍率は例外。</summary>
        [Test]
        public void Lifecycle_AndValidation()
        {
            var (clock, hub, services) = Build();
            Assert.AreSame(clock, services.Resolve<IGameClock>());
            Assert.Throws<HubException>(() => hub.PublishCommand(new SetTimeScaleCommand(-1f)));

            clock.Dispose();
            Assert.IsFalse(services.TryResolve<IGameClock>(out _), "窓口は返却済み");
            Assert.Throws<HubException>(() => hub.PublishCommand(new SetPausedCommand(true)),
                "処理者不在は握り潰されず検知される");
        }
    }
}
