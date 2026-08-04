using NUnit.Framework;
using Seed.Core;

namespace Seed.Core.Tests
{
    /// <summary>
    /// 基盤強化（統合スナップショット・スコープ購読・台帳の巻き戻し・コーデック防御）の検証。
    /// </summary>
    public sealed class HardeningTests
    {
        /// <summary>テスト用イベント。</summary>
        private sealed class ProbeEvent : LogicEvent
        {
            /// <summary>呼ばれた回数の記録先。</summary>
            public int Payload;

            /// <summary>状態を初期化する。</summary>
            public override void Reset()
            {
                Payload = 0;
                TargetId = 0;
            }
        }

        /// <summary>呼び出し回数を数えるだけのハンドラー。</summary>
        private sealed class CountingHandler : LogicEventHandlerBase, ILogicEventHandler<ProbeEvent>
        {
            /// <summary>呼ばれた回数。</summary>
            public int Calls;

            /// <summary>CountingHandler を生成する（無所属）。</summary>
            public CountingHandler() : base(owner: null)
            {
            }

            /// <summary>優先度（固定）。</summary>
            public override int Priority => 100;

            /// <summary>全体購読で登録する（スコープ購読はテスト側が直接行う）。</summary>
            public override void RegisterTo(EventHub hub) => hub.Subscribe<ProbeEvent>(this);

            /// <summary>回数を数える。</summary>
            public void Handle(ProbeEvent ev, LogicContext ctx)
            {
                Calls++;
            }
        }

        /// <summary>
        /// スコープ購読: 対象ID付きで購読したハンドラーは、その対象への発火にだけ反応する
        /// （1000体が他人のイベントで空呼び出しされるスケール問題への対策）。
        /// </summary>
        [Test]
        public void EventHub_ScopedSubscription_FiresOnlyForTarget()
        {
            var ctx = new LogicContext(1);
            var global = new CountingHandler();
            var forUnit7 = new CountingHandler();
            var forUnit8 = new CountingHandler();
            ctx.Hub.Subscribe<ProbeEvent>(global);
            ctx.Hub.Subscribe<ProbeEvent>(forUnit7, targetId: 7);
            ctx.Hub.Subscribe<ProbeEvent>(forUnit8, targetId: 8);

            var ev = new ProbeEvent();
            ctx.Hub.Fire(ev, ctx, targetId: 7);

            Assert.AreEqual(1, global.Calls, "全体購読者は常に呼ばれる");
            Assert.AreEqual(1, forUnit7.Calls, "対象7の購読者は呼ばれる");
            Assert.AreEqual(0, forUnit8.Calls, "対象8の購読者は呼ばれない");
        }

        /// <summary>
        /// 統合スナップショット: CaptureAll / RestoreAll で
        /// 時刻・乱数・購読・参加者（EntityRegistry）が一括で巻き戻る。
        /// </summary>
        [Test]
        public void WorldSnapshot_CaptureAndRestore_RewindsRegistry()
        {
            var ctx = new LogicContext(700);
            var registry = new EntityRegistry();
            ctx.AddSnapshotParticipant(registry);

            var first = registry.Register(new object());
            var snapshot = ctx.CaptureAll();
            var timeAtCapture = ctx.NowMs;

            // 巻き戻し対象になる変更: 時間前進＋新規スポーン
            ctx.AdvanceTime(5000);
            var spawned = registry.Register(new object());
            Assert.AreEqual(2, registry.Count);

            ctx.RestoreAll(snapshot);

            Assert.AreEqual(timeAtCapture, ctx.NowMs, "時刻が戻る");
            Assert.AreEqual(1, registry.Count, "スポーンが巻き戻る");
            var respawned = registry.Register(new object());
            Assert.AreEqual(spawned, respawned, "採番カーソルも戻る＝再シミュレーションでIDが分岐しない");
            Assert.IsNotNull(registry.GetEntity<object>(first), "既存の在籍は残っている");
        }

        /// <summary>RecordLog: TruncateTo は世代を進め、Truncated を発火する（消費者の巻き戻し追従用）。</summary>
        [Test]
        public void RecordLog_TruncateTo_BumpsGenerationAndNotifies()
        {
            var log = new RecordLog<int>();
            log.Add(1);
            log.Add(2);
            log.Add(3);
            var generationBefore = log.Generation;
            var notified = -1;
            log.Truncated += count => notified = count;

            log.TruncateTo(1);

            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(generationBefore + 1, log.Generation);
            Assert.AreEqual(1, notified, "切り詰め後の件数が通知される");
        }

        /// <summary>int入力の最小コーデック（テスト用）。</summary>
        private sealed class IntCodec : IInputCodec<int>
        {
            /// <summary>4バイトで書く。</summary>
            public void Write(System.IO.BinaryWriter writer, in int input)
            {
                writer.Write(input);
            }

            /// <summary>4バイトで読む。</summary>
            public int Read(System.IO.BinaryReader reader)
            {
                return reader.ReadInt32();
            }
        }

        /// <summary>コーデック: 壊れたバイト列は例外ではなく null で拒否される（契約どおり）。</summary>
        [Test]
        public void InputJournalCodec_RejectsCorruptBytes_WithNull()
        {
            var codec = new IntCodec();

            // 正常なバイト列を作ってから壊す
            var journal = new InputJournal<int> { Seed = 1, Version = 1 };
            journal.Record(0, 42);
            var bytes = InputJournalCodec.ToBytes(journal, codec);

            Assert.IsNotNull(InputJournalCodec.FromBytes(bytes, codec), "正常系は読める");

            // 途中で切る（EndOfStream 系）
            var cut = new byte[bytes.Length - 3];
            System.Array.Copy(bytes, cut, cut.Length);
            Assert.IsNull(InputJournalCodec.FromBytes(cut, codec), "途中切れは null");

            // ゴミ（magic 不一致）
            Assert.IsNull(InputJournalCodec.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, codec),
                "ゴミは null");
        }
    }
}
