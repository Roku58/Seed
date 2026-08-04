using NUnit.Framework;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>CharacterAgent のリアクション割り込み（PostReaction）のテスト。</summary>
    public sealed class AgentReactionTests
    {
        /// <summary>標準行動一式の Actor を1体持つ Agent を組む。</summary>
        private static CharacterAgent BuildAgent(FakeAvatar avatar)
        {
            var agent = new CharacterAgent(new CharacterId(1));
            var actor = new CharacterActor(new ActorKey(1), avatar);
            actor.AddBehavior(new IdleBehavior());
            actor.AddBehavior(new LocomotionBehavior(4f));
            actor.AddBehavior(new AttackBehavior(0.4f));
            actor.AddBehavior(new GuardBehavior());
            actor.AddBehavior(new HitBehavior(0.25f));
            actor.AddBehavior(new DeathBehavior());
            agent.AddActor(actor, activate: true);
            return agent;
        }

        /// <summary>Hit リアクションは即時に行動へ反映され、Avatar に遷移が届く。</summary>
        [Test]
        public void HitReaction_TransitionsImmediately()
        {
            var avatar = new FakeAvatar();
            var agent = BuildAgent(avatar);

            agent.PostReaction(ReactionId.Hit);

            Assert.AreEqual(BehaviorKey.Hit.Value, agent.ActiveActor.CurrentKey.Value, "Tickを待たずに反映");
            Assert.IsTrue(avatar.LastTransitionTo(BehaviorKey.Hit));
        }

        /// <summary>Death リアクションで IsAlive が落ち、以後の Hit は無視される。</summary>
        [Test]
        public void DeathReaction_MarksDead_AndIgnoresFurtherHits()
        {
            var avatar = new FakeAvatar();
            var agent = BuildAgent(avatar);

            agent.PostReaction(ReactionId.Death);
            Assert.IsFalse(agent.IsAlive);
            Assert.AreEqual(BehaviorKey.Death.Value, agent.ActiveActor.CurrentKey.Value);

            var transitionsBefore = avatar.Transitions.Count;
            agent.PostReaction(ReactionId.Hit);
            Assert.AreEqual(BehaviorKey.Death.Value, agent.ActiveActor.CurrentKey.Value, "死亡後はのけぞらない");
            Assert.AreEqual(transitionsBefore, avatar.Transitions.Count, "Avatar にも遷移が届かない");
        }

        /// <summary>GuardOn/GuardOff リアクションの往復（互換経路）。</summary>
        [Test]
        public void GuardReactions_ToggleGuardBehavior()
        {
            var agent = BuildAgent(new FakeAvatar());

            agent.PostReaction(ReactionId.GuardOn);
            Assert.AreEqual(BehaviorKey.Guard.Value, agent.ActiveActor.CurrentKey.Value);

            agent.PostReaction(ReactionId.GuardOff);
            Assert.AreEqual(BehaviorKey.Idle.Value, agent.ActiveActor.CurrentKey.Value);
        }

        /// <summary>死亡中の Agent を Tick しても意図は流れず、死亡姿勢が維持される。</summary>
        [Test]
        public void DeadAgent_TickKeepsDeathBehavior()
        {
            var agent = BuildAgent(new FakeAvatar());
            agent.PostReaction(ReactionId.Death);

            agent.SetIntent(new CharacterIntent(Vector3.forward, false, BehaviorKey.Attack, 0));
            agent.Tick(0.1f);

            Assert.AreEqual(BehaviorKey.Death.Value, agent.ActiveActor.CurrentKey.Value);
        }

        /// <summary>行動開始イベントが Id 付きで再発火される。</summary>
        [Test]
        public void BehaviorStarted_CarriesIdAndPayload()
        {
            var agent = BuildAgent(new FakeAvatar());
            CharacterBehaviorEvent? received = null;
            agent.BehaviorStarted += ev => received = ev;

            agent.SetIntent(new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 42));
            agent.Tick(0.1f);

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(1, received.Value.Id.Value);
            Assert.AreEqual(BehaviorKey.Attack.Value, received.Value.Behavior.Value);
            Assert.AreEqual(42, received.Value.Payload, "技IDが荷物として届く");
        }
    }
}
