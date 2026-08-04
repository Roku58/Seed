// ============================================================================
// Seed.Hub（MessageHub / ServiceRegistry）の EditMode テスト。
// 発行順・発行中の解除/登録・トークンDispose・サービス登録の各規約を検証する。
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Seed.Hub;

namespace Seed.Hub.Tests
{
    /// <summary>MessageHub と ServiceRegistry の基本規約テスト。</summary>
    public sealed class HubTests
    {
        /// <summary>テスト用のメッセージ。</summary>
        private readonly struct ProbeMessage
        {
            /// <summary>識別用の値。</summary>
            public readonly int Value;

            /// <summary>ProbeMessage を生成する。</summary>
            public ProbeMessage(int value)
            {
                Value = value;
            }
        }

        /// <summary>テスト用のサービスインターフェース。</summary>
        private interface IProbeService
        {
        }

        /// <summary>テスト用のサービス実装。</summary>
        private sealed class ProbeService : IProbeService
        {
        }

        /// <summary>優先度昇順・同値は登録順、の安定順序で配達される。</summary>
        [Test]
        public void Publish_CallsSubscribers_InPriorityThenRegistrationOrder()
        {
            var hub = new MessageHub();
            var trace = new List<string>();
            hub.Subscribe<ProbeMessage>(_ => trace.Add("B1"), priority: 200);
            hub.Subscribe<ProbeMessage>(_ => trace.Add("A"), priority: 100);
            hub.Subscribe<ProbeMessage>(_ => trace.Add("B2"), priority: 200);

            hub.Publish(new ProbeMessage(1));

            CollectionAssert.AreEqual(new[] { "A", "B1", "B2" }, trace);
        }

        /// <summary>発行中に自分をDisposeしても安全で、次回から配達されない。</summary>
        [Test]
        public void Dispose_DuringPublish_IsSafe_AndStopsNextDelivery()
        {
            var hub = new MessageHub();
            var trace = new List<string>();
            IDisposable once = null;
            once = hub.Subscribe<ProbeMessage>(_ =>
            {
                trace.Add("once");
                once.Dispose(); // きのみ消費と同型
            }, priority: 100);
            hub.Subscribe<ProbeMessage>(_ => trace.Add("keep"), priority: 200);

            hub.Publish(new ProbeMessage(1));
            hub.Publish(new ProbeMessage(2));

            CollectionAssert.AreEqual(new[] { "once", "keep", "keep" }, trace);
            Assert.AreEqual(1, hub.SubscriberCount(typeof(ProbeMessage)));
        }

        /// <summary>発行中に登録された購読者は、その発行では呼ばれず次回から呼ばれる。</summary>
        [Test]
        public void Subscribe_DuringPublish_IsDeferredToNextPublish()
        {
            var hub = new MessageHub();
            var trace = new List<string>();
            hub.Subscribe<ProbeMessage>(_ =>
            {
                trace.Add("A");
                hub.Subscribe<ProbeMessage>(_ => trace.Add("late"), priority: 0);
            }, priority: 100);

            hub.Publish(new ProbeMessage(1));
            CollectionAssert.AreEqual(new[] { "A" }, trace, "同一発行内では呼ばれない");

            trace.Clear();
            hub.Publish(new ProbeMessage(2));
            Assert.IsTrue(trace.Contains("late"), "次の発行からは呼ばれる");
        }

        /// <summary>二重Disposeは無害。</summary>
        [Test]
        public void Dispose_Twice_IsHarmless()
        {
            var hub = new MessageHub();
            var token = hub.Subscribe<ProbeMessage>(_ => { });

            token.Dispose();
            token.Dispose();

            Assert.AreEqual(0, hub.SubscriberCount(typeof(ProbeMessage)));
        }

        /// <summary>サービスの登録・解決・二重登録検知・未登録検知。</summary>
        [Test]
        public void ServiceRegistry_RegisterResolve_AndGuards()
        {
            var registry = new ServiceRegistry();
            var service = new ProbeService();

            registry.Register<IProbeService>(service);
            Assert.AreSame(service, registry.Resolve<IProbeService>());
            Assert.IsTrue(registry.TryResolve<IProbeService>(out _));

            Assert.Throws<HubException>(() => registry.Register<IProbeService>(new ProbeService()),
                "二重登録は構成ミスとして検知");
            registry.Register<IProbeService>(new ProbeService(), allowOverwrite: true);

            registry.Unregister<IProbeService>();
            Assert.Throws<HubException>(() => registry.Resolve<IProbeService>(), "未登録の解決は配線漏れとして検知");
        }
    }
}
