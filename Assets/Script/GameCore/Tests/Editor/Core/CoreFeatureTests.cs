// ============================================================================
// コア標準機能（v0.2 で追加）の EditMode テスト。
// EventScope / 遅延Subscribe / ConditionMergePolicy / スナップショット /
// RollAlwaysConsume / Fork / StableHash / EntityRegistry / FactoryRegistry /
// InputJournal / RecordLog.TruncateTo / トレース を検証する。
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;

namespace Seed.Core.Tests
{
    /// <summary>コア標準機能（スコープ・遅延購読・ポリシー・スナップショット等）のテスト。</summary>
    public sealed class CoreFeatureTests
    {
        // ---------------- EventScope ----------------

        /// <summary>EventScopeテスト用のダミーイベント。</summary>
        private sealed class ScopeProbeEvent : LogicEvent
        {
            /// <summary>主値（補正対象・ダメージ量など）。</summary>
            public int Value;

            /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
            public override void Reset()
            {
                Value = 0;
            }
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void EventScope_ReturnsToPool_AndResets()
        {
            using (var scope = EventScope<ScopeProbeEvent>.Rent())
            {
                scope.Event.Value = 123;
            }

            // 返却済みなので、次に借りるとリセット済みの同一インスタンスが来る
            var pooled = EventPool<ScopeProbeEvent>.Rent();
            Assert.AreEqual(0, pooled.Value);
            EventPool<ScopeProbeEvent>.Return(pooled);
        }

        // ---------------- EventHub: 発火中Subscribeの遅延 ----------------

        /// <summary>遅延購読テスト用のダミーイベント。</summary>
        private sealed class DeferProbeEvent : LogicEvent
        {
            /// <summary>呼び出し順の記録先（テスト用）。</summary>
            public List<string> Trace;

            /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
            public override void Reset()
            {
                Trace = null;
            }
        }

        /// <summary>発火中に別ハンドラーを登録するテスト用ハンドラー。</summary>
        private sealed class DeferHandler : LogicEventHandlerBase, ILogicEventHandler<DeferProbeEvent>
        {
            /// <summary>識別用ラベル（テスト用）。</summary>
            private readonly string _label;
            /// <summary>優先度（テスト用）。</summary>
            private readonly int _priority;
            /// <summary>反応時に追加登録するハンドラー生成（テスト用）。</summary>
            private readonly Func<LogicEventHandlerBase> _subscribeOnHandle;

            /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
            public override int Priority => _priority;

            /// <summary>DeferHandler を生成する。</summary>
            public DeferHandler(string label, int priority,
                Func<LogicEventHandlerBase> subscribeOnHandle = null) : base(null)
            {
                _label = label;
                _priority = priority;
                _subscribeOnHandle = subscribeOnHandle;
            }

            /// <summary>購読するイベントを登録する。</summary>
            public override void RegisterTo(EventHub hub) => hub.Subscribe<DeferProbeEvent>(this);

            /// <summary>イベントに反応する処理。</summary>
            public void Handle(DeferProbeEvent ev, LogicContext ctx)
            {
                ev.Trace.Add(_label);
                _subscribeOnHandle?.Invoke().RegisterTo(ctx.Hub);
            }
        }

        /// <summary>
        /// 発火中に登録された購読者は「その発火では呼ばれず」、次の発火から優先度順で呼ばれる。
        /// （優先度が低くても割り込まない＝挙動が決定的）
        /// </summary>
        [Test]
        public void EventHub_SubscribeDuringFire_IsDeferredToNextFire()
        {
            var ctx = new LogicContext(1);
            // A(優先度200)が発火中に B(優先度100) を登録する
            new DeferHandler("A", 200, () => new DeferHandler("B", 100)).RegisterTo(ctx.Hub);

            var trace = new List<string>();
            ctx.BeginResolution();
            ctx.Hub.Fire(new DeferProbeEvent { Trace = trace }, ctx);
            CollectionAssert.AreEqual(new[] { "A" }, trace, "同一発火内では B は呼ばれない");

            trace.Clear();
            ctx.Hub.Fire(new DeferProbeEvent { Trace = trace }, ctx);
            CollectionAssert.AreEqual(new[] { "B", "A" }, trace,
                "次の発火では優先度順（B=100 が先）。この発火中に A が登録した2体目の B はさらに次回から");
            Assert.AreEqual(3, ctx.Hub.SubscriberCount(typeof(DeferProbeEvent)),
                "A + B + 待機中の2体目B が購読者として数えられる");
        }

        // ---------------- ConditionMergePolicy ----------------

        /// <summary>テスト用のコンディション種別。</summary>
        private enum BuffKind
        {
            /// <summary>なし。</summary>
            None,
            /// <summary>攻撃技。</summary>
            Attack,
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ConditionSet_AddOrMerge_Extend_KeepsMagnitudeAndExtendsTime()
        {
            var set = new ConditionSet<BuffKind>();
            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 1000, 15), ConditionMergePolicy.Extend, 0);
            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 5000, 99), ConditionMergePolicy.Extend, 0);

            Assert.AreEqual(1, set.Count);
            set.TryGet(BuffKind.Attack, 0, out var condition);
            Assert.AreEqual(15, condition.Magnitude, "効果量は既存を維持");
            Assert.AreEqual(5000, condition.ExpiresAtMs, "時間は延長");
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ConditionSet_AddOrMerge_AddMagnitude_SumsMagnitude()
        {
            var set = new ConditionSet<BuffKind>();
            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 1000, 10), ConditionMergePolicy.AddMagnitude, 0);
            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 2000, 5), ConditionMergePolicy.AddMagnitude, 0);

            Assert.AreEqual(1, set.Count);
            set.TryGet(BuffKind.Attack, 0, out var condition);
            Assert.AreEqual(15, condition.Magnitude);
            Assert.AreEqual(2000, condition.ExpiresAtMs);
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ConditionSet_AddOrMerge_IgnoreAndOverwrite()
        {
            var set = new ConditionSet<BuffKind>();
            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 1000, 10), ConditionMergePolicy.Ignore, 0);
            var changed = set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 9999, 99), ConditionMergePolicy.Ignore, 0);
            Assert.IsFalse(changed, "Ignore は既存があれば何もしない");
            set.TryGet(BuffKind.Attack, 0, out var kept);
            Assert.AreEqual(10, kept.Magnitude);

            set.AddOrMerge(new TimedCondition<BuffKind>(BuffKind.Attack, 9999, 99), ConditionMergePolicy.Overwrite, 0);
            Assert.AreEqual(1, set.Count);
            set.TryGet(BuffKind.Attack, 0, out var overwritten);
            Assert.AreEqual(99, overwritten.Magnitude);
        }

        // ---------------- スナップショット ----------------

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void CoreSnapshot_RestoresTimeAndRngSequence()
        {
            var ctx = new LogicContext(7);
            ctx.AdvanceTime(1234);

            var snapshot = ctx.CaptureCoreSnapshot();
            var first = new[]
            {
                ctx.LogicRandom.NextPermille(),
                ctx.LogicRandom.NextPermille(),
                ctx.LogicRandom.NextPermille(),
            };
            ctx.AdvanceTime(9999);

            ctx.RestoreCoreSnapshot(in snapshot);
            Assert.AreEqual(1234, ctx.NowMs, "時刻が巻き戻る");
            var second = new[]
            {
                ctx.LogicRandom.NextPermille(),
                ctx.LogicRandom.NextPermille(),
                ctx.LogicRandom.NextPermille(),
            };
            CollectionAssert.AreEqual(first, second, "乱数列も巻き戻る");
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void RecordLog_TruncateTo_RollsBackRecords()
        {
            var log = new RecordLog<int>();
            log.Add(1);
            var mark = log.Count;
            log.Add(2);
            log.Add(3);

            log.TruncateTo(mark);
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(1, log[0]);
        }

        // ---------------- 乱数 ----------------

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void RollAlwaysConsume_ConsumesEvenOnZeroChance()
        {
            var a = new DeterministicRandom(123);
            var b = new DeterministicRandom(123);

            Assert.IsFalse(a.RollAlwaysConsume(0)); // 0%でも1回消費する
            b.NextPermille();                        // 手動で1回消費して同期
            Assert.AreEqual(b.NextPermille(), a.NextPermille());
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void Fork_CreatesIndependentDeterministicStream()
        {
            var parent1 = new DeterministicRandom(42);
            var parent2 = new DeterministicRandom(42);

            var child1 = parent1.Fork();
            var child2 = parent2.Fork();

            Assert.AreEqual(child1.NextPermille(), child2.NextPermille(), "同じ親からの Fork は同じ子になる（決定的）");
            Assert.AreEqual(parent1.NextPermille(), parent2.NextPermille(), "Fork 後も親同士は同期している");
        }

        // ---------------- StableHash ----------------

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void StableHash_SameInput_SameHash_DifferentOrder_DifferentHash()
        {
            var a = StableHash.Create();
            a.Add("ダメージ");
            a.Add(98);
            a.Add(true);
            var b = StableHash.Create();
            b.Add("ダメージ");
            b.Add(98);
            b.Add(true);
            Assert.AreEqual(a.Value, b.Value);

            var c = StableHash.Create();
            c.Add(98);
            c.Add("ダメージ");
            c.Add(true);
            Assert.AreNotEqual(a.Value, c.Value);
        }

        // ---------------- EntityRegistry / FactoryRegistry / InputJournal ----------------

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void EntityRegistry_AssignsStableSequentialIds()
        {
            var registry = new EntityRegistry();
            var actorA = new object();
            var actorB = new object();

            Assert.AreEqual(1, registry.Register(actorA));
            Assert.AreEqual(2, registry.Register(actorB));
            Assert.AreEqual(1, registry.Register(actorA), "再登録は同じID");
            Assert.AreSame(actorB, registry.GetEntity<object>(2));
            Assert.IsNull(registry.GetEntity<object>(EntityRegistry.None));
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void FactoryRegistry_CreatesOnlyBoundKeys()
        {
            var registry = new FactoryRegistry<string, int, string>();
            registry.Bind("double", n => (n * 2).ToString());

            Assert.IsTrue(registry.TryCreate("double", 21, out var product));
            Assert.AreEqual("42", product);
            Assert.IsFalse(registry.TryCreate("unknown", 1, out _), "未登録＝存在しない仕様");
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void InputJournal_RecordsInOrder()
        {
            var journal = new InputJournal<int>(4) { Seed = 1041, Version = 1 };
            journal.Record(0, 10);
            journal.Record(1000, 20);

            Assert.AreEqual(2, journal.Count);
            Assert.AreEqual(0, journal[0].TimeMs);
            Assert.AreEqual(20, journal[1].Input);
            Assert.Throws<ArgumentException>(() => journal.Record(500, 30), "時刻の逆行は弾く");
        }

        // ---------------- トレース ----------------

        /// <summary>トレース検証用の最小セクション。</summary>
        private sealed class CountingSection : Section<int, int>
        {
            /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
            public static readonly CountingSection Instance = new CountingSection();
            /// <summary>表示・デバッグ用の名前。</summary>
            public override string Name => "カウント";

            /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
            protected override int Execute(LogicContext ctx, in int input)
            {
                return input + 1;
            }
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void CoreTrace_ObservesSectionsAndEvents()
        {
            var ctx = new LogicContext(1);
            var trace = new BufferedCoreTrace();
            ctx.TraceListener = trace;

            ctx.BeginResolution();
            ctx.RunSection(CountingSection.Instance, 0);

            Assert.IsTrue(trace.Lines.Count > 0);
            StringAssert.Contains("カウント", trace.Lines[0]);
        }
    }
}
