// ============================================================================
// v0.3 で追加したコア機能のテスト:
// 購読スナップショット / 明示ID登録 / 入力コーデック / レコードカーソル /
// Bind重複ガード / トレース上限
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;
using AB = Seed.Core.Samples.ActionBattle;

namespace Seed.Core.Tests
{
    /// <summary>購読スナップショット・明示ID・コーデック・カーソル等のテスト。</summary>
    public sealed class SnapshotReplayTests
    {
        /// <summary>購読スナップショットテスト用のダミーイベント。</summary>
        private sealed class SnapProbeEvent : LogicEvent
        {
            /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
            public override void Reset()
            {
            }
        }

        /// <summary>「消費＝購読解除」型のテスト用ハンドラー。</summary>
        private sealed class ConsumableHandler : LogicEventHandlerBase,
            ILogicEventHandler<SnapProbeEvent>
        {
            /// <summary>反応した回数（テスト用）。</summary>
            public int HandledCount;

            /// <summary>ConsumableHandler を生成する。</summary>
            public ConsumableHandler() : base(null)
            {
            }

            /// <summary>購読するイベントを登録する。</summary>
            public override void RegisterTo(EventHub hub) => hub.Subscribe<SnapProbeEvent>(this);

            /// <summary>イベントに反応する処理。</summary>
            public void Handle(SnapProbeEvent ev, LogicContext ctx)
            {
                HandledCount++;
                ctx.Hub.Unsubscribe(typeof(SnapProbeEvent), this); // きのみ消費と同型
            }
        }

        /// <summary>購読スナップショット: 消費（解除）が巻き戻り、もう一度反応できる。</summary>
        [Test]
        public void SubscriptionSnapshot_RestoresConsumedSubscription()
        {
            var ctx = new LogicContext(1);
            var berry = new ConsumableHandler();
            berry.RegisterTo(ctx.Hub);

            var snapshot = ctx.Hub.CaptureSnapshot();

            ctx.BeginResolution();
            ctx.Hub.Fire(new SnapProbeEvent(), ctx); // 消費される
            Assert.AreEqual(0, ctx.Hub.SubscriberCount(typeof(SnapProbeEvent)));

            ctx.Hub.RestoreSnapshot(snapshot); // 巻き戻し

            Assert.AreEqual(1, ctx.Hub.SubscriberCount(typeof(SnapProbeEvent)));
            ctx.Hub.Fire(new SnapProbeEvent(), ctx);
            Assert.AreEqual(2, berry.HandledCount, "復元後にもう一度反応できる");
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void EntityRegistry_RegisterWithId_IsOrderIndependent_AndDetectsCollision()
        {
            var a = new object();
            var b = new object();

            var registry1 = new EntityRegistry();
            registry1.RegisterWithId(a, 10);
            registry1.RegisterWithId(b, 2);

            var registry2 = new EntityRegistry();
            registry2.RegisterWithId(b, 2); // 逆順でも
            registry2.RegisterWithId(a, 10);

            Assert.AreEqual(registry1.GetId(a), registry2.GetId(a));
            Assert.AreSame(a, registry2.GetEntity<object>(10));
            Assert.AreEqual(2, registry2.Count);

            Assert.Throws<LogicException>(() => registry1.RegisterWithId(new object(), 10),
                "ID衝突は構成ミスとして検知");
            Assert.Throws<LogicException>(() => registry1.RegisterWithId(a, 99),
                "同一エンティティの別ID登録も検知");
        }

        /// <summary>入力コーデック: byte[] へ保存 → 復元 → 再生で同一結果（リプレイ保存の完成形）。</summary>
        [Test]
        public void InputJournalCodec_RoundTrip_ReproducesIdenticalResult()
        {
            var ctx = AB.Sample_ActionBattleDemo.RunDemo();
            var journal = ctx.GetExtension<InputJournal<AB.Sample_ActionInput>>();
            var codec = new AB.Sample_ActionInputCodec();

            var bytes = InputJournalCodec.ToBytes(journal, codec);
            var loaded = InputJournalCodec.FromBytes(bytes, codec, expectedVersion: 1);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(journal.Seed, loaded.Seed);
            Assert.AreEqual(journal.Count, loaded.Count);

            // 復元したジャーナルで新しい世界を再生 → 同一ハッシュ
            var world = new AB.Sample_ActionWorld(loaded.Seed, trace: null);
            for (var i = 0; i < loaded.Count; i++)
            {
                world.Driver.Execute(loaded[i].Input);
            }
            Assert.AreEqual(AB.Sample_ActionBattleDemo.HashRecords(ctx),
                AB.Sample_ActionBattleDemo.HashRecords(world.Ctx));
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void InputJournalCodec_RejectsWrongVersion()
        {
            var journal = new InputJournal<AB.Sample_ActionInput>(4) { Seed = 1, Version = 2 };
            var codec = new AB.Sample_ActionInputCodec();
            var bytes = InputJournalCodec.ToBytes(journal, codec);

            Assert.IsNull(InputJournalCodec.FromBytes(bytes, codec, expectedVersion: 1));
            Assert.IsNotNull(InputJournalCodec.FromBytes(bytes, codec, expectedVersion: 2));
        }

        /// <summary>レコードカーソル: 複数の消費者が独立に「新着分だけ」読める。</summary>
        [Test]
        public void RecordLog_TryRead_SupportsIndependentCursors()
        {
            var log = new RecordLog<int>();
            log.Add(10);
            log.Add(20);

            var presentationCursor = 0;
            var achievementCursor = 0;

            Assert.IsTrue(log.TryRead(ref presentationCursor, out var first));
            Assert.AreEqual(10, first);
            Assert.IsTrue(log.TryRead(ref presentationCursor, out _));
            Assert.IsFalse(log.TryRead(ref presentationCursor, out _), "追いついたら false");

            log.Add(30);
            Assert.IsTrue(log.TryRead(ref presentationCursor, out var third));
            Assert.AreEqual(30, third);

            var drained = 0;
            while (log.TryRead(ref achievementCursor, out _)) drained++;
            Assert.AreEqual(3, drained, "別カーソルは独立して最初から読める");
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void FactoryRegistry_DuplicateBind_Throws_UnlessOverwriteAllowed()
        {
            var registry = new FactoryRegistry<string, int, string>();
            registry.Bind("key", n => n.ToString());

            Assert.Throws<LogicException>(() => registry.Bind("key", n => "dup"));
            registry.Bind("key", n => "replaced", allowOverwrite: true);
            registry.TryCreate("key", 1, out var product);
            Assert.AreEqual("replaced", product);
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void BufferedCoreTrace_StopsAtMaxLines()
        {
            var trace = new BufferedCoreTrace(maxLines: 3);
            for (var i = 0; i < 10; i++)
            {
                trace.OnEventFired(typeof(SnapProbeEvent), 1);
            }
            Assert.AreEqual(4, trace.Lines.Count, "上限3行＋上限到達マーカー1行");
        }
    }
}
