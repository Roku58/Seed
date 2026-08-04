using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;
using Seed.Hub;

namespace Seed.App.Tests
{
    /// <summary>合成ルートの骨格（TickPipeline / CompositionScope / Translator / Funnel）のテスト。</summary>
    public sealed class FoundationTests
    {
        /// <summary>テスト用のレコード（種別＋値）。</summary>
        private readonly struct ProbeRecord
        {
            /// <summary>種別。</summary>
            public readonly ProbeKind Kind;

            /// <summary>値。</summary>
            public readonly int Value;

            /// <summary>ProbeRecord を生成する。</summary>
            public ProbeRecord(ProbeKind kind, int value)
            {
                Kind = kind;
                Value = value;
            }
        }

        /// <summary>テスト用のレコード種別。</summary>
        private enum ProbeKind
        {
            /// <summary>翻訳対象。</summary>
            Mapped,
            /// <summary>明示無視。</summary>
            Ignored,
            /// <summary>表に無い。</summary>
            Unknown,
        }

        /// <summary>パイプラインはフェーズ昇順→登録順で実行する。</summary>
        [Test]
        public void TickPipeline_RunsPhasesInOrder()
        {
            var pipeline = new TickPipeline();
            var order = new List<string>();
            // わざと逆順で登録しても、実行はフェーズ順
            pipeline.Add(TickPhase.Drain, _ => order.Add("drain"));
            pipeline.Add(TickPhase.LogicTime, _ => order.Add("logic"));
            pipeline.Add(TickPhase.Simulation, _ => order.Add("sim"));
            pipeline.Add(TickPhase.Input, _ => order.Add("input"));
            pipeline.Add(TickPhase.Drain, _ => order.Add("drain2")); // 同フェーズは登録順

            pipeline.Tick(0.1f);

            CollectionAssert.AreEqual(
                new[] { "input", "sim", "logic", "drain", "drain2" }, order);
        }

        /// <summary>スコープは生成の逆順で破棄する。</summary>
        [Test]
        public void CompositionScope_DisposesInReverseOrder()
        {
            var scope = new CompositionScope();
            var order = new List<string>();
            scope.Own(new ProbeDisposable(() => order.Add("first")));
            scope.Own(new ProbeDisposable(() => order.Add("second")));

            scope.Dispose();
            scope.Dispose(); // 二重Disposeは無害

            CollectionAssert.AreEqual(new[] { "second", "first" }, order);
        }

        /// <summary>翻訳表: Map は翻訳、MapIgnore は静かに捨て、表に無い種は Unmapped へ通知される。</summary>
        [Test]
        public void RecordHubTranslator_MapsIgnoresAndReportsUnmapped()
        {
            var log = new RecordLog<ProbeRecord>();
            var translated = new List<int>();
            var unmapped = new List<ProbeKind>();

            var translator = new RecordHubTranslator<ProbeRecord, ProbeKind>(r => r.Kind)
                .Map(ProbeKind.Mapped, (in ProbeRecord r) => translated.Add(r.Value))
                .MapIgnore(ProbeKind.Ignored);
            translator.Unmapped += kind => unmapped.Add(kind);
            translator.Attach(log);

            log.Add(new ProbeRecord(ProbeKind.Mapped, 10));
            log.Add(new ProbeRecord(ProbeKind.Ignored, 20));
            log.Add(new ProbeRecord(ProbeKind.Unknown, 30));
            var count = translator.Drain();

            Assert.AreEqual(3, count, "3件とも消費される");
            CollectionAssert.AreEqual(new[] { 10 }, translated, "翻訳されるのは Map した種だけ");
            CollectionAssert.AreEqual(new[] { ProbeKind.Unknown }, unmapped, "表に無い種は黙殺されず通知される");
        }

        /// <summary>翻訳表の二重登録は構成ミスとして例外。</summary>
        [Test]
        public void RecordHubTranslator_DuplicateMap_Throws()
        {
            var translator = new RecordHubTranslator<ProbeRecord, ProbeKind>(r => r.Kind)
                .Map(ProbeKind.Mapped, (in ProbeRecord r) => { });
            Assert.Throws<HubException>(() => translator.MapIgnore(ProbeKind.Mapped));
        }

        /// <summary>ファネルは「記録してから実行」の順を守る。</summary>
        [Test]
        public void LogicInputFunnel_RecordsBeforeExecute()
        {
            var journal = new InputJournal<int>();
            var countAtExecute = -1;
            var funnel = new LogicInputFunnel<int>(journal,
                (in int input) => countAtExecute = journal.Count, () => 0L);

            funnel.Submit(42);

            Assert.AreEqual(1, journal.Count);
            Assert.AreEqual(1, countAtExecute, "実行の時点で既に記録済み（クラッシュしても記録が残る）");
            Assert.AreEqual(42, journal[0].Input);
        }

        /// <summary>Dispose 時に処理を差し込めるテスト用ダミー。</summary>
        private sealed class ProbeDisposable : System.IDisposable
        {
            /// <summary>Dispose 時に呼ぶ処理。</summary>
            private readonly System.Action _onDispose;

            /// <summary>ProbeDisposable を生成する。</summary>
            public ProbeDisposable(System.Action onDispose)
            {
                _onDispose = onDispose;
            }

            /// <summary>差し込まれた処理を実行する。</summary>
            public void Dispose()
            {
                _onDispose();
            }
        }
    }
}
