using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>CharacterRegistry（名簿・ICharacterQuery/ICharacterRoster）と Manager 群・Factory のテスト。</summary>
    public sealed class CharacterRegistryTests
    {
        /// <summary>標準行動一式の Agent を組む。</summary>
        private static CharacterAgent BuildAgent(int id, FakeAvatar avatar = null)
        {
            return CharacterFactory.Create(new CharacterId(id), new CharacterDefinition(),
                new ActorBlueprint(new ActorKey(1), avatar ?? new FakeAvatar()));
        }

        /// <summary>制御つきユニットを組む。</summary>
        private static UnitController BuildController(int id, FakeAvatar avatar = null)
        {
            return new PlayerController(BuildAgent(id, avatar), new ManualLogic());
        }

        /// <summary>登録で在籍し、取得できる。</summary>
        [Test]
        public void Register_ThenTryGet_Succeeds()
        {
            var registry = new CharacterRegistry();
            var agent = BuildAgent(1);
            registry.Register(agent, FactionId.Players);

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(new CharacterId(1), out var found));
            Assert.AreSame(agent, found);
            Assert.AreEqual(FactionId.Players.Value, registry.GetFaction(new CharacterId(1)).Value);
        }

        /// <summary>同一IDの二重登録は構成ミスとして例外。</summary>
        [Test]
        public void DuplicateRegister_Throws()
        {
            var registry = new CharacterRegistry();
            registry.Register(BuildAgent(1), FactionId.Players);
            Assert.Throws<HubException>(() => registry.Register(BuildAgent(1), FactionId.Enemies));
        }

        /// <summary>抹消後はすべての問い合わせが false / None。</summary>
        [Test]
        public void Unregister_MakesQueriesFail()
        {
            var registry = new CharacterRegistry();
            registry.Register(BuildAgent(1), FactionId.Players);
            Assert.IsTrue(registry.Unregister(new CharacterId(1)));

            Assert.IsFalse(registry.TryGet(new CharacterId(1), out _));
            Assert.IsFalse(registry.IsAlive(new CharacterId(1)));
            Assert.IsFalse(registry.TryGetPosition(new CharacterId(1), out _));
            Assert.AreEqual(FactionId.None.Value, registry.GetFaction(new CharacterId(1)).Value);
        }

        /// <summary>IsAlive は Agent の生存写しを反映する。</summary>
        [Test]
        public void IsAlive_ReflectsAgentState()
        {
            var registry = new CharacterRegistry();
            var agent = BuildAgent(1);
            registry.Register(agent, FactionId.Players);

            Assert.IsTrue(registry.IsAlive(new CharacterId(1)));
            agent.PostReaction(ReactionId.Death);
            Assert.IsFalse(registry.IsAlive(new CharacterId(1)));
        }

        /// <summary>位置問い合わせは表示中 Actor の Pose を契約層のベクトルで返す（純C#で完結）。</summary>
        [Test]
        public void TryGetPosition_ReturnsActiveActorPose()
        {
            var registry = new CharacterRegistry();
            var agent = BuildAgent(1);
            registry.Register(agent, FactionId.Players);
            agent.ActiveActor.Pose.Position = new Vector3(3f, 1f, -2f);

            Assert.IsTrue(registry.TryGetPosition(new CharacterId(1), out var position));
            Assert.AreEqual(new HubVector3(3f, 1f, -2f), position);
        }

        /// <summary>列挙は陣営・生存で絞れ、在籍順で返る（ターゲット選択AIの土台）。</summary>
        [Test]
        public void Query_FiltersByFactionAndAlive()
        {
            var registry = new CharacterRegistry();
            var player = BuildAgent(1);
            var enemyA = BuildAgent(2);
            var enemyB = BuildAgent(3);
            registry.Register(player, FactionId.Players);
            registry.Register(enemyA, FactionId.Enemies);
            registry.Register(enemyB, FactionId.Enemies);
            enemyA.PostReaction(ReactionId.Death);

            var buffer = new CharacterId[8];

            Assert.AreEqual(2, registry.Query(FactionId.Enemies, aliveOnly: false, buffer), "敵は2体");
            Assert.AreEqual(2, buffer[0].Value, "在籍順");

            Assert.AreEqual(1, registry.Query(FactionId.Enemies, aliveOnly: true, buffer), "生存の敵は1体");
            Assert.AreEqual(3, buffer[0].Value);

            Assert.AreEqual(3, registry.Query(FactionId.Any, aliveOnly: false, buffer), "Any は全員");
        }

        /// <summary>Manager.Add は名簿へ陣営付きで登録し、Remove は名簿抹消＋表示物の解放まで行う。</summary>
        [Test]
        public void UnitManager_AddRemove_SyncsRegistryAndReleasesAvatar()
        {
            var registry = new CharacterRegistry();
            var players = new PlayersManager(registry);
            var avatar = new FakeAvatar();

            players.Add(BuildController(1, avatar));
            Assert.AreEqual(1, registry.Count, "Manager に居る = 名簿に居る");
            Assert.AreEqual(FactionId.Players.Value, registry.GetFaction(new CharacterId(1)).Value,
                "陣営は Manager が付与する");

            Assert.IsTrue(players.Remove(new CharacterId(1)));
            Assert.AreEqual(0, registry.Count);
            Assert.IsTrue(avatar.Released, "退場で表示物まで一気通貫で解放される（リーク防止）");
        }

        /// <summary>CharactersManager は陣営を登録順に Tick する（全体管理）。</summary>
        [Test]
        public void CharactersManager_TicksFactionsInOrder()
        {
            var registry = new CharacterRegistry();
            var characters = new CharactersManager(registry);
            var players = new PlayersManager(registry);
            var enemies = new EnemiesManager(registry);
            characters.AddManager(players);
            characters.AddManager(enemies);

            var playerAvatar = new FakeAvatar();
            var enemyAvatar = new FakeAvatar();
            players.Add(BuildController(1, playerAvatar));
            enemies.Add(new EnemyController(BuildAgent(2, enemyAvatar), NullLogic.Instance));

            // Activate 時の即時反映ぶんを除いた差分で「1Tickで1回駆動」を確認する
            var playerBefore = playerAvatar.ApplyPoseCount;
            var enemyBefore = enemyAvatar.ApplyPoseCount;
            characters.Tick(0.1f);

            Assert.AreEqual(playerBefore + 1, playerAvatar.ApplyPoseCount, "プレイヤー陣営が駆動された");
            Assert.AreEqual(enemyBefore + 1, enemyAvatar.ApplyPoseCount, "エネミー陣営も駆動された");
        }

        /// <summary>CharactersManager は陣営をまたいでユニットを探せる。</summary>
        [Test]
        public void CharactersManager_FindsControllerAcrossFactions()
        {
            var registry = new CharacterRegistry();
            var characters = new CharactersManager(registry);
            var players = new PlayersManager(registry);
            var enemies = new EnemiesManager(registry);
            characters.AddManager(players);
            characters.AddManager(enemies);
            players.Add(BuildController(1));
            enemies.Add(new EnemyController(BuildAgent(2), NullLogic.Instance));

            Assert.IsTrue(characters.TryGetController(new CharacterId(2), out var found));
            Assert.AreEqual(2, found.Id.Value);
            Assert.IsFalse(characters.TryGetController(new CharacterId(9), out _));
        }

        /// <summary>Clear で全陣営の全ユニットが名簿から退場し、表示物も解放される。</summary>
        [Test]
        public void CharactersManager_Clear_EmptiesRegistry()
        {
            var registry = new CharacterRegistry();
            var characters = new CharactersManager(registry);
            var players = new PlayersManager(registry);
            var enemies = new EnemiesManager(registry);
            characters.AddManager(players);
            characters.AddManager(enemies);
            var avatar = new FakeAvatar();
            players.Add(BuildController(1, avatar));
            enemies.Add(new EnemyController(BuildAgent(2), NullLogic.Instance));

            characters.Clear();
            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(0, players.Count);
            Assert.AreEqual(0, enemies.Count);
            Assert.IsTrue(avatar.Released);
        }

        /// <summary>Factory はレシピどおりに組み立て、独自行動も装着できる（増やしやすさの検証）。</summary>
        [Test]
        public void CharacterFactory_AssemblesFromDefinition()
        {
            var definition = new CharacterDefinition { MoveSpeed = 6f }
                .WithBehavior(() => new ProbeBehavior());
            var avatar = new FakeAvatar();

            var agent = CharacterFactory.Create(new CharacterId(1), definition,
                new ActorBlueprint(new ActorKey(1), avatar));

            Assert.AreEqual(BehaviorKey.Idle.Value, agent.ActiveActor.CurrentKey.Value, "Idle で立ち上がる");
            Assert.IsTrue(agent.ActiveActor.RequestBehavior(new BehaviorKey(100)), "独自行動が装着済み");

            // プール再利用: 死亡状態からの再出発
            agent.PostReaction(ReactionId.Death);
            Assert.IsFalse(agent.IsAlive);
            CharacterFactory.ResetForReuse(agent);
            Assert.IsTrue(agent.IsAlive, "生存写しが立て直る");
            Assert.AreEqual(BehaviorKey.Idle.Value, agent.ActiveActor.CurrentKey.Value, "Idle から再出発");
        }

        /// <summary>独自行動のダミー（キー100）。</summary>
        private sealed class ProbeBehavior : CharacterBehaviorBase
        {
            /// <summary>この行動のキー。</summary>
            public override BehaviorKey Key => new BehaviorKey(100);
        }
    }
}
