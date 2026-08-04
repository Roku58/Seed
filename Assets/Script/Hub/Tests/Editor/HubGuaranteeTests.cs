// ============================================================================
// MessageHub に追加した「規約をコードで守らせる」機構のテスト。
// 命令の処理者1基盤 / 例外隔離 / 遅延発行 / 循環検知 / 観測フック / 購読袋。
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Seed.Hub.Contracts;

namespace Seed.Hub.Tests
{
    /// <summary>MessageHub の規約強制・堅牢化のテスト。</summary>
    public sealed class HubGuaranteeTests
    {
        /// <summary>テスト用の通知メッセージ。</summary>
        private readonly struct ProbeNotification : INotificationMessage
        {
            /// <summary>識別用の値。</summary>
            public readonly int Value;

            /// <summary>ProbeNotification を生成する。</summary>
            public ProbeNotification(int value)
            {
                Value = value;
            }
        }

        /// <summary>テスト用の命令メッセージ。</summary>
        private readonly struct ProbeCommand : ICommandMessage
        {
            /// <summary>識別用の値。</summary>
            public readonly int Value;

            /// <summary>ProbeCommand を生成する。</summary>
            public ProbeCommand(int value)
            {
                Value = value;
            }
        }

        // ---------------- 命令は処理者1基盤（規約の強制） ----------------

        /// <summary>命令の二重購読は規約違反として例外。</summary>
        [Test]
        public void SubscribeCommand_Twice_Throws()
        {
            var hub = new MessageHub();
            hub.SubscribeCommand<ProbeCommand>(_ => { });

            Assert.Throws<HubException>(() => hub.SubscribeCommand<ProbeCommand>(_ => { }),
                "命令の処理者は1基盤なので二重購読は弾かれる");
        }

        /// <summary>処理者のいない命令の発行は配線漏れとして例外（黙って握り潰さない）。</summary>
        [Test]
        public void PublishCommand_WithoutHandler_Throws()
        {
            var hub = new MessageHub();
            Assert.Throws<HubException>(() => hub.PublishCommand(new ProbeCommand(1)));
        }

        /// <summary>命令型を通常の Subscribe/Publish に渡すと例外（誤用を実行時に必ず露見させる）。</summary>
        [Test]
        public void CommandThroughNotificationApi_Throws()
        {
            var hub = new MessageHub();
            Assert.Throws<HubException>(() => hub.Subscribe<ProbeCommand>(_ => { }));
            Assert.Throws<HubException>(() => hub.Publish(new ProbeCommand(1)));
        }

        /// <summary>処理者を解除した後は同じ命令を再登録できる（一時的な基盤の差し替えが成立する）。</summary>
        [Test]
        public void SubscribeCommand_AfterDispose_Succeeds()
        {
            var hub = new MessageHub();
            var first = hub.SubscribeCommand<ProbeCommand>(_ => { });
            first.Dispose();

            Assert.DoesNotThrow(() => hub.SubscribeCommand<ProbeCommand>(_ => { }));
        }

        // ---------------- 例外の隔離 ----------------

        /// <summary>1購読者の例外でも後続へ必ず配達され、集約例外として投げ直される。</summary>
        [Test]
        public void Publish_OneHandlerThrows_StillDeliversToOthers()
        {
            var hub = new MessageHub();
            var trace = new List<string>();
            hub.Subscribe<ProbeNotification>(_ => trace.Add("first"), priority: 100);
            hub.Subscribe<ProbeNotification>(_ => throw new InvalidOperationException("bug"), priority: 200);
            hub.Subscribe<ProbeNotification>(_ => trace.Add("third"), priority: 300);

            var error = Assert.Throws<HubException>(() => hub.Publish(new ProbeNotification(1)));

            Assert.AreEqual(2, trace.Count, "例外を出した購読者の前後は配達される");
            Assert.AreEqual("first", trace[0]);
            Assert.AreEqual("third", trace[1]);
            Assert.IsInstanceOf<AggregateException>(error.InnerException, "元の例外が包まれている");
        }

        /// <summary>DeliveryFailed を購読していれば通知のみで、発行は例外にならない。</summary>
        [Test]
        public void DeliveryFailed_Subscribed_SwallowsException()
        {
            var hub = new MessageHub();
            var failures = new List<Type>();
            hub.DeliveryFailed += (type, _) => failures.Add(type);
            hub.Subscribe<ProbeNotification>(_ => throw new InvalidOperationException("bug"));

            Assert.DoesNotThrow(() => hub.Publish(new ProbeNotification(1)));
            Assert.AreEqual(1, failures.Count);
            Assert.AreEqual(typeof(ProbeNotification), failures[0]);
        }

        // ---------------- 遅延発行（連鎖の外へ逃がす） ----------------

        /// <summary>遅延発行は Pump まで配達されない。</summary>
        [Test]
        public void PublishDeferred_DeliversOnPump()
        {
            var hub = new MessageHub();
            var received = new List<int>();
            hub.Subscribe<ProbeNotification>(m => received.Add(m.Value));

            hub.PublishDeferred(new ProbeNotification(7));
            Assert.AreEqual(0, received.Count, "Pump までは配達されない");
            Assert.AreEqual(1, hub.DeferredCount);

            var delivered = hub.Pump();
            Assert.AreEqual(1, delivered);
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(7, received[0]);
            Assert.AreEqual(0, hub.DeferredCount);
        }

        /// <summary>Pump 中の遅延発行は次回の Pump へ回る（無限ループにしない）。</summary>
        [Test]
        public void PublishDeferred_DuringPump_GoesToNextPump()
        {
            var hub = new MessageHub();
            var received = new List<int>();
            hub.Subscribe<ProbeNotification>(m =>
            {
                received.Add(m.Value);
                if (m.Value < 2)
                {
                    hub.PublishDeferred(new ProbeNotification(m.Value + 1));
                }
            });

            hub.PublishDeferred(new ProbeNotification(1));
            hub.Pump();
            Assert.AreEqual(1, received.Count, "1回目の Pump では1件だけ");

            hub.Pump();
            Assert.AreEqual(2, received.Count, "2回目の Pump で続きが配達される");
        }

        // ---------------- 循環発行の検知 ----------------

        /// <summary>入れ子発行が上限を超えたら循環として例外（スタックオーバーフローの前に落とす）。</summary>
        [Test]
        public void Publish_CyclicChain_ThrowsAtDepthLimit()
        {
            var hub = new MessageHub();
            hub.Subscribe<ProbeNotification>(m => hub.Publish(new ProbeNotification(m.Value + 1)));

            var error = Assert.Throws<HubException>(() => hub.Publish(new ProbeNotification(0)));
            Assert.IsTrue(error.Message.Contains("循環") || error.InnerException != null,
                "循環検知の例外が（集約されて）伝わる");
        }

        // ---------------- 観測フック ----------------

        /// <summary>発行フックで型と購読者数が観測できる（購読者0でも観測される）。</summary>
        [Test]
        public void MessagePublished_ObservesTypeAndSubscriberCount()
        {
            var hub = new MessageHub();
            var observed = new List<(Type Type, int Count)>();
            hub.MessagePublished += (type, count) => observed.Add((type, count));

            hub.Publish(new ProbeNotification(1)); // 購読者0
            hub.Subscribe<ProbeNotification>(_ => { });
            hub.Publish(new ProbeNotification(2)); // 購読者1

            Assert.AreEqual(2, observed.Count);
            Assert.AreEqual(0, observed[0].Count, "購読者0の発行も観測できる（握り潰しの調査用）");
            Assert.AreEqual(1, observed[1].Count);
            Assert.AreEqual(typeof(ProbeNotification), observed[1].Type);
        }

        // ---------------- 購読袋 ----------------

        /// <summary>袋の Dispose で預けた購読が一括解除される。</summary>
        [Test]
        public void SubscriptionBag_DisposesAllSubscriptions()
        {
            var hub = new MessageHub();
            var bag = new SubscriptionBag();
            var received = 0;
            hub.Subscribe<ProbeNotification>(_ => received++).AddTo(bag);
            hub.Subscribe<ProbeNotification>(_ => received++).AddTo(bag);

            hub.Publish(new ProbeNotification(1));
            Assert.AreEqual(2, received);

            bag.Dispose();
            hub.Publish(new ProbeNotification(2));
            Assert.AreEqual(2, received, "解除後は配達されない");
            Assert.AreEqual(0, hub.SubscriberCount(typeof(ProbeNotification)));
        }

        /// <summary>破棄済みの袋へ入れた購読は即座に解除される（破棄後に購読が生き残らない）。</summary>
        [Test]
        public void SubscriptionBag_AddAfterDispose_DisposesImmediately()
        {
            var hub = new MessageHub();
            var bag = new SubscriptionBag();
            bag.Dispose();

            var received = 0;
            hub.Subscribe<ProbeNotification>(_ => received++).AddTo(bag);

            hub.Publish(new ProbeNotification(1));
            Assert.AreEqual(0, received);
        }

        // ---------------- サービス台帳の規約 ----------------

        /// <summary>実装型でのサービス登録は規約違反として例外（契約だけを貸し借りする）。</summary>
        [Test]
        public void ServiceRegistry_RegisterConcreteType_Throws()
        {
            var services = new ServiceRegistry();
            Assert.Throws<HubException>(() => services.Register(new ProbeService()));
        }

        /// <summary>テスト用のサービスインターフェース。</summary>
        private interface IProbeService
        {
        }

        /// <summary>テスト用のサービス実装。</summary>
        private sealed class ProbeService : IProbeService
        {
        }
    }
}
