// ============================================================================
// コア基礎動作の EditMode テスト（発火順・解除・暴走ガード・コンディション・乱数規約）。
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;

namespace Seed.Core.Tests
{
    /// <summary>コア基礎動作（発火順・解除・暴走ガード・コンディション・乱数規約）のテスト。</summary>
    public sealed class CoreBasicsTests
    {
        /// <summary>コア基礎テスト用のダミーイベント。</summary>
        private sealed class CoreProbeEvent : LogicEvent
        {
            /// <summary>呼び出し順の記録先（テスト用）。</summary>
            public List<string> Trace;

            /// <summary>プール返却時に全フィールドを初期状態へ戻す。</summary>
            public override void Reset()
            {
                Trace = null;
            }
        }

        /// <summary>発火順・解除テスト用のダミーハンドラー。</summary>
        private sealed class CoreProbeHandler : LogicEventHandlerBase, ILogicEventHandler<CoreProbeEvent>
        {
            /// <summary>識別用ラベル（テスト用）。</summary>
            private readonly string _label;
            /// <summary>優先度（テスト用）。</summary>
            private readonly int _priority;
            /// <summary>反応時に自分を解除するか（テスト用）。</summary>
            private readonly bool _unsubscribeSelf;

            /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
            public override int Priority => _priority;

            /// <summary>CoreProbeHandler を生成する。</summary>
            public CoreProbeHandler(string label, int priority, bool unsubscribeSelf = false, object owner = null)
                : base(owner)
            {
                _label = label;
                _priority = priority;
                _unsubscribeSelf = unsubscribeSelf;
            }

            /// <summary>購読するイベントを登録する。</summary>
            public override void RegisterTo(EventHub hub) => hub.Subscribe<CoreProbeEvent>(this);

            /// <summary>イベントに反応する処理。</summary>
            public void Handle(CoreProbeEvent ev, LogicContext ctx)
            {
                ev.Trace.Add(_label);
                if (_unsubscribeSelf)
                {
                    ctx.Hub.Unsubscribe(typeof(CoreProbeEvent), this); // 発火中の解除（墓標方式）
                }
            }
        }

        /// <summary>優先度昇順・同値は登録順、の安定順序で発火する。</summary>
        [Test]
        public void EventHub_FiresInPriorityThenRegistrationOrder()
        {
            var ctx = new LogicContext(1);
            new CoreProbeHandler("B1", 200).RegisterTo(ctx.Hub);
            new CoreProbeHandler("A", 100).RegisterTo(ctx.Hub);
            new CoreProbeHandler("B2", 200).RegisterTo(ctx.Hub);
            new CoreProbeHandler("C", 300).RegisterTo(ctx.Hub);

            var trace = new List<string>();
            ctx.BeginResolution();
            ctx.Hub.Fire(new CoreProbeEvent { Trace = trace }, ctx);

            CollectionAssert.AreEqual(new[] { "A", "B1", "B2", "C" }, trace);
        }

        /// <summary>発火中の購読解除（きのみ消費など）が安全に動き、次回は呼ばれない。</summary>
        [Test]
        public void EventHub_UnsubscribeDuringFire_IsSafe()
        {
            var ctx = new LogicContext(1);
            new CoreProbeHandler("once", 100, unsubscribeSelf: true).RegisterTo(ctx.Hub);
            new CoreProbeHandler("keep", 200).RegisterTo(ctx.Hub);

            var trace = new List<string>();
            ctx.BeginResolution();
            ctx.Hub.Fire(new CoreProbeEvent { Trace = trace }, ctx);
            ctx.Hub.Fire(new CoreProbeEvent { Trace = trace }, ctx);

            CollectionAssert.AreEqual(new[] { "once", "keep", "keep" }, trace);
            Assert.AreEqual(1, ctx.Hub.SubscriberCount(typeof(CoreProbeEvent)));
        }

        /// <summary>所有者インデックスによる一括解除。</summary>
        [Test]
        public void EventHub_UnsubscribeAll_RemovesOwnersHandlers()
        {
            var ctx = new LogicContext(1);
            var owner = new object();
            new CoreProbeHandler("o1", 100, owner: owner).RegisterTo(ctx.Hub);
            new CoreProbeHandler("o2", 200, owner: owner).RegisterTo(ctx.Hub);
            new CoreProbeHandler("other", 300, owner: new object()).RegisterTo(ctx.Hub);

            ctx.Hub.UnsubscribeAll(owner);

            Assert.AreEqual(1, ctx.Hub.SubscriberCount(typeof(CoreProbeEvent)));
        }

        /// <summary>深度ガード検証用の無限再帰セクション。</summary>
        private sealed class CoreRecursiveSection : Section<int, int>
        {
            /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
            public static readonly CoreRecursiveSection Instance = new CoreRecursiveSection();
            /// <summary>表示・デバッグ用の名前。</summary>
            public override string Name => "無限再帰テスト";

            /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
            protected override int Execute(LogicContext ctx, in int input)
            {
                /// <summary>セクションを実行する。</summary>
                return ctx.RunSection(Instance, input + 1); // わざと無限再帰
            }
        }

        /// <summary>セクション深度の暴走ガードが働く。</summary>
        [Test]
        public void RunSection_ExceedingDepth_Throws()
        {
            var ctx = new LogicContext(1);
            Assert.Throws<LogicException>(() => ctx.RunSection(CoreRecursiveSection.Instance, 0));
        }

        /// <summary>テスト用のコンディション種別。</summary>
        private enum CoreKind
        {
            /// <summary>なし。</summary>
            None,
            /// <summary>テスト用バフ。</summary>
            Buff,
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ConditionSet_ExpiresByTime_AndRemove()
        {
            var set = new ConditionSet<CoreKind>();
            set.Add(new TimedCondition<CoreKind>(CoreKind.Buff, expiresAtMs: 1000, magnitude: 15));

            Assert.IsTrue(set.Has(CoreKind.Buff, nowMs: 999));
            Assert.IsFalse(set.Has(CoreKind.Buff, nowMs: 1000)); // 失効時刻ちょうどで無効

            var expired = new List<TimedCondition<CoreKind>>();
            Assert.AreEqual(1, set.RemoveExpired(1000, expired));
            Assert.AreEqual(15, expired[0].Magnitude);
            Assert.AreEqual(0, set.Count);
        }

        /// <summary>確率0‰/1000‰では乱数を消費しない（消費数の決定性）。</summary>
        [Test]
        public void DeterministicRandom_DoesNotConsume_OnZeroOrCertain()
        {
            var a = new DeterministicRandom(123);
            var b = new DeterministicRandom(123);

            Assert.IsFalse(a.Roll(0));
            Assert.IsTrue(a.Roll(1000));
            // a は一度も消費していないので、b と同じ値を返すはず
            Assert.AreEqual(b.NextPermille(), a.NextPermille());
        }
    }
}
