using System.Collections.Generic;
using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Flow.Tests
{
    /// <summary>GameFlow（フェーズ遷移状態機械）のテスト。すべて純C#。</summary>
    public sealed class GameFlowTests
    {
        /// <summary>テスト用フェーズID。</summary>
        private static readonly PhaseId Home = new PhaseId(1);

        /// <summary>テスト用フェーズID。</summary>
        private static readonly PhaseId Battle = new PhaseId(2);

        /// <summary>手動で完了させられるロード作業。</summary>
        private sealed class ManualOperation : IFlowOperation
        {
            /// <summary>完了したか（テストが立てる）。</summary>
            public bool IsDone { get; set; }

            /// <summary>進捗。</summary>
            public float Progress => IsDone ? 1f : 0.5f;
        }

        /// <summary>出入りと駆動を記録するプローブフェーズ。</summary>
        private sealed class ProbePhase : GamePhase
        {
            /// <summary>このフェーズのID。</summary>
            private readonly PhaseId _id;

            /// <summary>入場時に返すロード作業（null なら即時）。</summary>
            public IFlowOperation LoadOperation;

            /// <summary>呼び出し履歴（"enter:payload" / "exit" / "tick"）。</summary>
            public readonly List<string> Log = new List<string>();

            /// <summary>ProbePhase を生成する。</summary>
            public ProbePhase(PhaseId id)
            {
                _id = id;
            }

            /// <summary>このフェーズのID。</summary>
            public override PhaseId Id => _id;

            /// <summary>設定されたロード作業を返す。</summary>
            public override IFlowOperation CreateLoadOperation(int payload)
            {
                return LoadOperation;
            }

            /// <summary>入場を記録する。</summary>
            public override void OnEnter(int payload)
            {
                Log.Add($"enter:{payload}");
            }

            /// <summary>退場を記録する。</summary>
            public override void OnExit()
            {
                Log.Add("exit");
            }

            /// <summary>駆動を記録する。</summary>
            public override void Tick(float deltaTime)
            {
                Log.Add("tick");
            }
        }

        /// <summary>2フェーズ構成のフローを組む。</summary>
        private static (GameFlow Flow, ProbePhase Home, ProbePhase Battle, MessageHub Hub) Build()
        {
            var hub = new MessageHub();
            var flow = new GameFlow(hub);
            var home = new ProbePhase(Home);
            var battle = new ProbePhase(Battle);
            flow.AddPhase(home);
            flow.AddPhase(battle);
            return (flow, home, battle, hub);
        }

        /// <summary>Start 予約 → 次の Tick で入場し、以後駆動される。</summary>
        [Test]
        public void Start_EntersInitialPhase_OnNextTick()
        {
            var (flow, home, _, _) = Build();
            flow.Start(Home, payload: 7);
            Assert.AreEqual(PhaseId.None.Value, flow.Current.Value, "Tick 前は未入場");

            flow.Tick(0.1f); // 退場なし→即時入場（このフレームは駆動しない）
            flow.Tick(0.1f);

            CollectionAssert.AreEqual(new[] { "enter:7", "tick" }, home.Log);
            Assert.AreEqual(Home.Value, flow.Current.Value);
        }

        /// <summary>ChangePhaseCommand（Hub命令）で遷移し、PhaseChanged が発行される。</summary>
        [Test]
        public void ChangePhaseCommand_SwitchesPhase_AndNotifies()
        {
            var (flow, home, battle, hub) = Build();
            var changes = new List<PhaseChanged>();
            hub.Subscribe<PhaseChanged>(m => changes.Add(m));
            flow.Start(Home);
            flow.Tick(0.1f);

            hub.PublishCommand(new ChangePhaseCommand(Battle, payload: 201));
            flow.Tick(0.1f); // 退場
            flow.Tick(0.1f); // 入場

            Assert.AreEqual("exit", home.Log[home.Log.Count - 1], "旧フェーズは退場済み");
            Assert.AreEqual("enter:201", battle.Log[0], "荷物（ステージID）付きで入場");
            Assert.AreEqual(2, changes.Count, "初回入場と遷移の2回");
            Assert.AreEqual(Home.Value, changes[1].Previous.Value);
            Assert.AreEqual(Battle.Value, changes[1].Current.Value);
            Assert.AreEqual(201, changes[1].Payload);
        }

        /// <summary>同一フェーズへの再入も Exit→Enter が完全に回る（＝ステージ切り替えの標準経路）。</summary>
        [Test]
        public void SamePhaseReentry_RunsFullExitEnterCycle()
        {
            var (flow, _, battle, _) = Build();
            flow.Start(Battle, payload: 201);
            flow.Tick(0.1f);

            flow.RequestChange(Battle, payload: 202); // ステージ切り替え
            flow.Tick(0.1f); // 入場(201)→即座に退場（要求済みのため）
            flow.Tick(0.1f); // 入場(202)→駆動

            CollectionAssert.AreEqual(new[] { "enter:201", "exit", "enter:202", "tick" }, battle.Log);
        }

        /// <summary>非同期ロード中はどのフェーズも駆動されず、完了後に入場する。</summary>
        [Test]
        public void AsyncLoad_BlocksTick_UntilDone()
        {
            var (flow, home, battle, _) = Build();
            var loading = new ManualOperation();
            battle.LoadOperation = loading;
            flow.Start(Home);
            flow.Tick(0.1f);

            flow.RequestChange(Battle);
            flow.Tick(0.1f); // Home 退場・ロード開始
            flow.Tick(0.1f); // ロード中
            flow.Tick(0.1f); // ロード中

            Assert.IsTrue(flow.IsTransitioning);
            Assert.AreEqual(0, battle.Log.Count, "ロード完了まで入場しない");
            Assert.AreEqual("exit", home.Log[home.Log.Count - 1], "旧フェーズは退場済み");
            Assert.AreEqual(0.5f, flow.LoadProgress, "進捗が読める（ローディング表示用）");

            loading.IsDone = true;
            flow.Tick(0.1f); // 入場
            Assert.AreEqual("enter:0", battle.Log[0]);
            Assert.IsFalse(flow.IsTransitioning);
        }

        /// <summary>フェーズ自身の Tick 中の遷移要求も安全（遅延実行）。</summary>
        [Test]
        public void ChangeRequestedDuringPhaseTick_IsDeferred()
        {
            var hub = new MessageHub();
            var flow = new GameFlow(hub);
            var battle = new ProbePhase(Battle);
            var home = new SelfChangingPhase(Home, flow);
            flow.AddPhase(home);
            flow.AddPhase(battle);

            flow.Start(Home);
            flow.Tick(0.1f); // 入場
            flow.Tick(0.1f); // Tick 中に自分で Battle を要求（この場では何も起きない）
            Assert.AreEqual(Home.Value, flow.Current.Value, "要求したフレームでは未遷移");

            flow.Tick(0.1f); // 退場
            flow.Tick(0.1f); // 入場
            Assert.AreEqual(Battle.Value, flow.Current.Value);
        }

        /// <summary>未登録フェーズへの要求は打ち間違いとして例外。</summary>
        [Test]
        public void UnknownPhase_Throws()
        {
            var (flow, _, _, _) = Build();
            Assert.Throws<HubException>(() => flow.RequestChange(new PhaseId(99)));
        }

        /// <summary>Dispose は滞在中フェーズを退場させ、以後の命令にも反応しない。</summary>
        [Test]
        public void Dispose_ExitsCurrentPhase()
        {
            var (flow, home, _, hub) = Build();
            flow.Start(Home);
            flow.Tick(0.1f); // 遷移開始
            flow.Tick(0.1f); // 入場

            flow.Dispose();

            Assert.AreEqual("exit", home.Log[home.Log.Count - 1]);
            // 命令の処理者が居なくなったことも検知される（握り潰し防止）
            Assert.Throws<HubException>(() => hub.PublishCommand(new ChangePhaseCommand(Battle)));
        }

        /// <summary>自分の Tick 中に遷移を要求するフェーズ。</summary>
        private sealed class SelfChangingPhase : GamePhase
        {
            /// <summary>このフェーズのID。</summary>
            private readonly PhaseId _id;

            /// <summary>要求先のフロー。</summary>
            private readonly GameFlow _flow;

            /// <summary>要求済みか。</summary>
            private bool _requested;

            /// <summary>SelfChangingPhase を生成する。</summary>
            public SelfChangingPhase(PhaseId id, GameFlow flow)
            {
                _id = id;
                _flow = flow;
            }

            /// <summary>このフェーズのID。</summary>
            public override PhaseId Id => _id;

            /// <summary>最初の Tick で Battle への遷移を要求する。</summary>
            public override void Tick(float deltaTime)
            {
                if (!_requested)
                {
                    _requested = true;
                    _flow.RequestChange(new PhaseId(2));
                }
            }
        }
    }
}
