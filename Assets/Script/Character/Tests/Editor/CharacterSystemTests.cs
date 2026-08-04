using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character.Tests
{
    /// <summary>CharacterSystem（Hub玄関）のテスト。実物の MessageHub / ServiceRegistry を使う。</summary>
    public sealed class CharacterSystemTests
    {
        /// <summary>標準行動一式の Agent を組む。</summary>
        private static CharacterAgent BuildAgent(int id, FakeAvatar avatar)
        {
            var agent = new CharacterAgent(new CharacterId(id));
            var actor = new CharacterActor(new ActorKey(1), avatar);
            actor.AddBehavior(new IdleBehavior());
            actor.AddBehavior(new HitBehavior(0.25f));
            actor.AddBehavior(new DeathBehavior());
            agent.AddActor(actor, activate: true);
            return agent;
        }

        /// <summary>全体管理＋玄関を組む（registry へ直接在籍させる簡易合成）。</summary>
        private static (CharacterSystem System, CharactersManager Characters, MessageHub Hub, ServiceRegistry Services)
            BuildSystem()
        {
            var hub = new MessageHub();
            var services = new ServiceRegistry();
            var characters = new CharactersManager(new CharacterRegistry());
            var system = new CharacterSystem();
            system.Initialize(hub, services, characters);
            return (system, characters, hub, services);
        }

        /// <summary>PlayReactionCommand が行動割り込みまで一周する。</summary>
        [Test]
        public void PlayReaction_ReachesBehaviorTransition()
        {
            var (system, characters, hub, _) = BuildSystem();
            var avatar = new FakeAvatar();
            characters.Registry.Register(BuildAgent(1, avatar), FactionId.Players);

            hub.PublishCommand(new PlayReactionCommand(new CharacterId(1), ReactionId.Hit));

            Assert.IsTrue(avatar.LastTransitionTo(BehaviorKey.Hit), "命令が Avatar の遷移まで届く");
            system.Dispose();
        }

        /// <summary>死亡命令で生存写しも落ちる。</summary>
        [Test]
        public void DeathReaction_UpdatesAliveQuery()
        {
            var (system, characters, hub, _) = BuildSystem();
            characters.Registry.Register(BuildAgent(1, new FakeAvatar()), FactionId.Players);

            Assert.IsTrue(characters.Registry.IsAlive(new CharacterId(1)));
            hub.PublishCommand(new PlayReactionCommand(new CharacterId(1), ReactionId.Death));
            Assert.IsFalse(characters.Registry.IsAlive(new CharacterId(1)), "問い合わせ窓口にも死亡が映る");
            system.Dispose();
        }

        /// <summary>退場済みキャラへの命令は静かに無視される（演出遅延の正常系）。</summary>
        [Test]
        public void ReactionToUnknownCharacter_IsIgnoredSilently()
        {
            var (system, _, hub, _) = BuildSystem();

            Assert.DoesNotThrow(() =>
                hub.PublishCommand(new PlayReactionCommand(new CharacterId(9), ReactionId.Hit)));
            system.Dispose();
        }

        /// <summary>Initialize で ICharacterQuery / ICharacterRoster が貸し出される。</summary>
        [Test]
        public void Initialize_LendsQueryAndRoster()
        {
            var (system, characters, _, services) = BuildSystem();

            Assert.AreSame(characters.Registry, services.Resolve<ICharacterQuery>());
            Assert.AreSame(characters.Registry, services.Resolve<ICharacterRoster>());
            system.Dispose();
        }

        /// <summary>Dispose 後の命令は「処理者不在」として即例外になり（握り潰し検知）、窓口も返却される。</summary>
        [Test]
        public void Dispose_UnsubscribesAndReturnsQuery()
        {
            var (system, characters, hub, services) = BuildSystem();
            characters.Registry.Register(BuildAgent(1, new FakeAvatar()), FactionId.Players);

            system.Dispose();

            // 命令は処理者1基盤の規約——解除済みへの発行は黙って消えず、その場で露見する
            Assert.Throws<HubException>(() =>
                hub.PublishCommand(new PlayReactionCommand(new CharacterId(1), ReactionId.Hit)));
            Assert.IsFalse(services.TryResolve<ICharacterQuery>(out _), "窓口は返却済み");
            Assert.IsFalse(services.TryResolve<ICharacterRoster>(out _), "列挙窓口も返却済み");
        }

        /// <summary>
        /// Tick 中に届いたリアクションは Tick 完了後に一括適用される（2フェーズ規約）。
        /// 「既に Tick 済みのユニットと未 Tick のユニットで観測結果が変わる」事故の防止。
        /// </summary>
        [Test]
        public void ReactionDuringTick_IsDeferredUntilAfterTick()
        {
            var (system, characters, hub, _) = BuildSystem();
            var players = new PlayersManager(characters.Registry);
            characters.AddManager(players);

            // ユニット1（先にTickされる側）と、Tick中にリアクションを発行するユニット2
            var avatar = new FakeAvatar();
            var agent = BuildAgent(1, avatar);
            players.Add(new PlayerController(agent, new ManualLogic()));

            var applied = false;
            var probe = new ProbeLogic(() =>
            {
                // ユニット2の Think（=Tickの最中）から、Tick済みのユニット1へ命令を出す
                characters.PostReaction(new CharacterId(1), ReactionId.Hit);
                applied = agent.ActiveActor.CurrentKey.Equals(BehaviorKey.Hit);
            });
            players.Add(new EnemyControllerLike(BuildAgent(2, new FakeAvatar()), probe));

            characters.Tick(0.1f);

            Assert.IsFalse(applied, "Tick の最中には適用されていない");
            Assert.AreEqual(BehaviorKey.Hit.Value, agent.ActiveActor.CurrentKey.Value,
                "Tick 完了後に一括適用されている");
            system.Dispose();
        }

        /// <summary>Think のタイミングで任意のコードを差し込むテスト用 Logic。</summary>
        private sealed class ProbeLogic : ICharacterLogic
        {
            /// <summary>Think 時に呼ぶ処理。</summary>
            private readonly System.Action _onThink;

            /// <summary>ProbeLogic を生成する。</summary>
            public ProbeLogic(System.Action onThink)
            {
                _onThink = onThink;
            }

            /// <summary>差し込まれた処理を実行して空の意図を返す。</summary>
            public CharacterIntent Think(in LogicFrame frame)
            {
                _onThink();
                return CharacterIntent.None;
            }
        }

        /// <summary>任意 Logic を受けるテスト用 Controller（EnemyController 相当）。</summary>
        private sealed class EnemyControllerLike : UnitController
        {
            /// <summary>EnemyControllerLike を生成する。</summary>
            public EnemyControllerLike(CharacterAgent agent, ICharacterLogic logic)
                : base(agent, logic)
            {
            }
        }
    }
}
