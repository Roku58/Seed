using NUnit.Framework;
using Seed.AI;
using Seed.Character;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.AI.Tests
{
    /// <summary>Seed.AI（頭脳・指示書・トークン・入力エミュレート）のテスト。すべて純C#。</summary>
    public sealed class AiFoundationTests
    {
        /// <summary>固定スコアと固定意図を返すテスト用の思考候補。</summary>
        private sealed class ProbeConsideration : IAiConsideration
        {
            /// <summary>返すスコア。</summary>
            public float ScoreValue;

            /// <summary>返す意図の行動キー。</summary>
            public BehaviorKey Action = BehaviorKey.None;

            /// <summary>Act が呼ばれた回数。</summary>
            public int ActCount;

            /// <summary>固定スコア。</summary>
            public float Score(in AiContext context)
            {
                return ScoreValue;
            }

            /// <summary>固定意図。</summary>
            public CharacterIntent Act(in AiContext context)
            {
                ActCount++;
                return new CharacterIntent(Vector3.zero, false, Action, 0);
            }
        }

        /// <summary>指示書を読んで従うテスト用の思考候補（攻撃権チェックの実例）。</summary>
        private sealed class ObedientAttack : IAiConsideration
        {
            /// <summary>許可されているときだけ参加する。</summary>
            public float Score(in AiContext context)
            {
                return context.Orders.AttackPermitted ? 100f : 0f;
            }

            /// <summary>攻撃の意図。</summary>
            public CharacterIntent Act(in AiContext context)
            {
                return new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 0);
            }
        }

        /// <summary>固定の指示書を返すテスト用ディレクター。</summary>
        private sealed class ProbeDirector : AiDirector
        {
            /// <summary>発行する指示書。</summary>
            public void Issue(CharacterId id, in AiOrders orders)
            {
                SetOrders(id, in orders);
            }

            /// <summary>何もしない。</summary>
            public override void Tick(float deltaTime)
            {
            }
        }

        /// <summary>問い合わせ窓口のダミー（このテスト群では中身を使わない）。</summary>
        private sealed class EmptyWorld : ICharacterQuery, ICharacterRoster
        {
            /// <summary>常に不在。</summary>
            public bool IsAlive(CharacterId id) => false;

            /// <summary>常に不在。</summary>
            public bool TryGetPosition(CharacterId id, out HubVector3 position)
            {
                position = HubVector3.Zero;
                return false;
            }

            /// <summary>在籍0。</summary>
            public int Count => 0;

            /// <summary>常に0件。</summary>
            public int Query(FactionId faction, bool aliveOnly, CharacterId[] buffer) => 0;

            /// <summary>常に無所属。</summary>
            public FactionId GetFaction(CharacterId id) => FactionId.None;
        }

        /// <summary>自分の LogicFrame を作る補助。</summary>
        private static LogicFrame Frame(int id = 1, float dt = 0.1f)
        {
            return new LogicFrame(new CharacterId(id), Vector3.zero, Quaternion.identity, true, dt);
        }

        /// <summary>最高得点の候補が採用され、同点は登録順で先勝ちする（決定的）。</summary>
        [Test]
        public void Brain_PicksHighestScore_FirstWinsTies()
        {
            var world = new EmptyWorld();
            var low = new ProbeConsideration { ScoreValue = 10f, Action = BehaviorKey.Guard };
            var first = new ProbeConsideration { ScoreValue = 50f, Action = BehaviorKey.Attack };
            var tied = new ProbeConsideration { ScoreValue = 50f, Action = BehaviorKey.Hit };
            var brain = new AiBrain(world, world).With(low).With(first).With(tied);

            var intent = brain.Think(Frame());

            Assert.AreEqual(BehaviorKey.Attack.Value, intent.RequestedAction.Value, "最高得点＋同点先勝ち");
            Assert.AreEqual(1, first.ActCount);
            Assert.AreEqual(0, tied.ActCount, "同点の後着は採用されない");
        }

        /// <summary>全員不参加（スコア0以下）なら何もしない意図になる。</summary>
        [Test]
        public void Brain_NoParticipants_ReturnsNone()
        {
            var world = new EmptyWorld();
            var brain = new AiBrain(world, world)
                .With(new ProbeConsideration { ScoreValue = 0f })
                .With(new ProbeConsideration { ScoreValue = -5f });

            var intent = brain.Think(Frame());

            Assert.AreEqual(BehaviorKey.None.Value, intent.RequestedAction.Value);
            Assert.AreEqual(Vector3.zero, intent.MoveDirection);
        }

        /// <summary>AI時計は Think のたびに進む（クールダウンの基準になる）。</summary>
        [Test]
        public void Brain_AdvancesBlackboardClock()
        {
            var world = new EmptyWorld();
            var brain = new AiBrain(world, world);
            brain.Think(Frame(dt: 0.5f));
            brain.Think(Frame(dt: 0.25f));
            Assert.AreEqual(0.75f, brain.Blackboard.TimeSeconds, 0.0001f);
        }

        /// <summary>ディレクターの指示書が思考へ届き、攻撃権で行動が変わる（メタAIの采配経路）。</summary>
        [Test]
        public void Director_OrdersGateConsiderations()
        {
            var world = new EmptyWorld();
            var director = new ProbeDirector();
            var brain = new AiBrain(world, world, director).With(new ObedientAttack());
            var id = new CharacterId(1);

            director.Issue(id, new AiOrders(attackPermitted: false, 1000, CharacterId.None));
            Assert.AreEqual(BehaviorKey.None.Value, brain.Think(Frame()).RequestedAction.Value, "不許可なら攻めない");

            director.Issue(id, new AiOrders(attackPermitted: true, 1000, CharacterId.None));
            Assert.AreEqual(BehaviorKey.Attack.Value, brain.Think(Frame()).RequestedAction.Value, "許可で攻める");
        }

        /// <summary>未発行のユニットへの指示は全許可（メタAI無しでも自律で動く）。</summary>
        [Test]
        public void Director_DefaultOrders_ArePermissive()
        {
            var director = new ProbeDirector();
            var orders = director.GetOrders(new CharacterId(9));
            Assert.IsTrue(orders.AttackPermitted);
            Assert.AreEqual(1000, orders.AggressionPermille);
        }

        /// <summary>攻撃権トークンは上限で頭打ちになり、返却で次が取れる。</summary>
        [Test]
        public void TokenPool_EnforcesConcurrencyLimit()
        {
            var pool = new AttackTokenPool(maxConcurrent: 2);
            var a = new CharacterId(1);
            var b = new CharacterId(2);
            var c = new CharacterId(3);

            Assert.IsTrue(pool.TryAcquire(a));
            Assert.IsTrue(pool.TryAcquire(a), "保持済みの再取得は成功扱い（二重カウントしない）");
            Assert.IsTrue(pool.TryAcquire(b));
            Assert.IsFalse(pool.TryAcquire(c), "満員");
            Assert.AreEqual(2, pool.ActiveCount);

            Assert.IsTrue(pool.Release(a));
            Assert.IsTrue(pool.TryAcquire(c), "返却で空きができる");
        }

        /// <summary>
        /// 入力エミュレート: AIの意図が手動入力の置き場（ManualLogic）を経由しても等価に再現される
        /// ＝プレイヤーとAIの制御経路が完全に共通であることの証明。
        /// </summary>
        [Test]
        public void InputEmulator_ReproducesIntentThroughManualLogic()
        {
            var manual = new ManualLogic();
            var emulator = new InputEmulator(manual);
            var aiIntent = new CharacterIntent(
                new Vector3(1f, 0f, 0f), guardHeld: true, BehaviorKey.Attack, 42);

            emulator.Drive(in aiIntent);
            var replayed = manual.Think(Frame());

            Assert.AreEqual(aiIntent.MoveDirection, replayed.MoveDirection);
            Assert.IsTrue(replayed.GuardHeld);
            Assert.AreEqual(BehaviorKey.Attack.Value, replayed.RequestedAction.Value);
            Assert.AreEqual(42, replayed.ActionPayload, "技IDまで素通し");

            // 解除すると移動・ガードが止まる（人間の入力へ返す）
            emulator.ReleaseAll();
            var released = manual.Think(Frame());
            Assert.AreEqual(Vector3.zero, released.MoveDirection);
            Assert.IsFalse(released.GuardHeld);
            Assert.AreEqual(BehaviorKey.None.Value, released.RequestedAction.Value, "予約行動は前回消費済み");
        }
    }
}
