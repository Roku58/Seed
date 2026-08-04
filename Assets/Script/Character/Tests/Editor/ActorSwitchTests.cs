using NUnit.Framework;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>CharacterAgent の複数 Actor 切替（SwitchActor）のテスト。</summary>
    public sealed class ActorSwitchTests
    {
        /// <summary>標準行動一式の Actor を組む。</summary>
        private static CharacterActor BuildActor(int key, FakeAvatar avatar)
        {
            var actor = new CharacterActor(new ActorKey(key), avatar);
            actor.AddBehavior(new IdleBehavior());
            actor.AddBehavior(new LocomotionBehavior(4f));
            actor.AddBehavior(new AttackBehavior(0.4f));
            actor.AddBehavior(new GuardBehavior());
            actor.AddBehavior(new HitBehavior(0.25f));
            actor.AddBehavior(new DeathBehavior());
            return actor;
        }

        /// <summary>2枚の Actor を持つ Agent を組む（1が表示中）。</summary>
        private static (CharacterAgent Agent, FakeAvatar A, FakeAvatar B) BuildAgent()
        {
            var agent = new CharacterAgent(new CharacterId(1));
            var avatarA = new FakeAvatar();
            var avatarB = new FakeAvatar();
            agent.AddActor(BuildActor(1, avatarA), activate: true);
            agent.AddActor(BuildActor(2, avatarB));
            return (agent, avatarA, avatarB);
        }

        /// <summary>非表示で登録した Actor の Avatar は隠される。</summary>
        [Test]
        public void InactiveActor_AvatarIsHidden()
        {
            var (agent, avatarA, avatarB) = BuildAgent();
            Assert.IsTrue(avatarA.IsActive);
            Assert.IsFalse(avatarB.IsActive, "控えの Actor は隠れている");
        }

        /// <summary>切替で姿勢が引き継がれ、新 Avatar に即時反映される。</summary>
        [Test]
        public void Switch_CarriesPose_AndAppliesImmediately()
        {
            var (agent, _, avatarB) = BuildAgent();

            // 移動して位置を作る
            agent.SetIntent(new CharacterIntent(Vector3.forward, false, BehaviorKey.None, 0));
            agent.Tick(1f);
            var movedPosition = agent.ActiveActor.Pose.Position;
            Assert.Greater(movedPosition.z, 0f);

            Assert.IsTrue(agent.SwitchActor(new ActorKey(2)));

            Assert.AreEqual(2, agent.ActiveActor.Key.Value);
            Assert.AreEqual(movedPosition, agent.ActiveActor.Pose.Position, "位置が引き継がれる");
            Assert.AreEqual(movedPosition, avatarB.LastPosition, "切替の瞬間に姿勢が反映される（表示ズレなし）");
            Assert.IsTrue(avatarB.IsActive);
        }

        /// <summary>切替で旧 Actor は隠れ、行動が初期化される（行動の途中経過は引き継がない）。</summary>
        [Test]
        public void Switch_DeactivatesOldActor_AndResetsBehavior()
        {
            var (agent, avatarA, _) = BuildAgent();

            // 攻撃の振り中に切り替える
            agent.SetIntent(new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 0));
            agent.Tick(0.1f);
            var oldActor = agent.ActiveActor;
            Assert.AreEqual(BehaviorKey.Attack.Value, oldActor.CurrentKey.Value);

            agent.SwitchActor(new ActorKey(2));

            Assert.IsFalse(avatarA.IsActive, "旧 Avatar は隠れる");
            Assert.IsFalse(oldActor.IsActive);
            Assert.AreEqual(BehaviorKey.None.Value, oldActor.CurrentKey.Value, "旧 Actor の行動は降ろされる");
            Assert.AreEqual(BehaviorKey.Idle.Value, agent.ActiveActor.CurrentKey.Value,
                "新 Actor は Idle から始まる（攻撃の続きはしない）");
        }

        /// <summary>死亡後の切替では新 Actor が Death で立ち上がる。</summary>
        [Test]
        public void SwitchAfterDeath_StartsInDeathBehavior()
        {
            var (agent, _, _) = BuildAgent();
            agent.PostReaction(ReactionId.Death);

            agent.SwitchActor(new ActorKey(2));

            Assert.AreEqual(BehaviorKey.Death.Value, agent.ActiveActor.CurrentKey.Value, "死んだまま表現が替わる");
            Assert.IsFalse(agent.IsAlive);
        }

        /// <summary>未知キー・同一キーへの切替は false。</summary>
        [Test]
        public void Switch_ToUnknownOrSameKey_ReturnsFalse()
        {
            var (agent, _, _) = BuildAgent();
            Assert.IsFalse(agent.SwitchActor(new ActorKey(99)), "未知キー");
            Assert.IsFalse(agent.SwitchActor(new ActorKey(1)), "同一キー");
        }

        /// <summary>切替後の行動開始イベントは新 Actor 由来で発火する。</summary>
        [Test]
        public void BehaviorStarted_AfterSwitch_ComesFromNewActor()
        {
            var (agent, _, _) = BuildAgent();
            agent.SwitchActor(new ActorKey(2));

            CharacterBehaviorEvent? received = null;
            agent.BehaviorStarted += ev => received = ev;

            agent.SetIntent(new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 7));
            agent.Tick(0.1f);

            Assert.IsTrue(received.HasValue, "新 Actor の遷移が届く");
            Assert.AreEqual(7, received.Value.Payload);
            Assert.AreEqual(BehaviorKey.Attack.Value, agent.ActiveActor.CurrentKey.Value);
        }

        /// <summary>同一 Actor キーの二重登録は構成ミスとして例外。</summary>
        [Test]
        public void DuplicateActorKey_Throws()
        {
            var agent = new CharacterAgent(new CharacterId(1));
            agent.AddActor(BuildActor(1, new FakeAvatar()), activate: true);
            Assert.Throws<Seed.Hub.HubException>(() => agent.AddActor(BuildActor(1, new FakeAvatar())));
        }
    }
}
