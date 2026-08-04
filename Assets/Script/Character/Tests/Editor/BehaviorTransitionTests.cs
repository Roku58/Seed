using NUnit.Framework;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>Behavior 状態機械（CharacterActor の遷移裁定）のテスト。</summary>
    public sealed class BehaviorTransitionTests
    {
        /// <summary>標準行動一式を備えた Actor を組む。</summary>
        private static CharacterActor BuildActor(FakeAvatar avatar,
            float attackSeconds = 0.4f, float staggerSeconds = 0.25f)
        {
            var actor = new CharacterActor(new ActorKey(1), avatar);
            actor.AddBehavior(new IdleBehavior());
            actor.AddBehavior(new LocomotionBehavior(4f));
            actor.AddBehavior(new AttackBehavior(attackSeconds));
            actor.AddBehavior(new GuardBehavior());
            actor.AddBehavior(new HitBehavior(staggerSeconds));
            actor.AddBehavior(new DeathBehavior());
            actor.Activate(BehaviorKey.Idle);
            return actor;
        }

        /// <summary>意図を渡して1Tick進める補助。</summary>
        private static void TickWith(CharacterActor actor, CharacterIntent intent, float dt = 0.1f)
        {
            actor.SetIntent(in intent);
            actor.Tick(dt);
        }

        /// <summary>起動直後は Idle。</summary>
        [Test]
        public void InitialBehavior_IsIdle()
        {
            var actor = BuildActor(new FakeAvatar());
            Assert.AreEqual(BehaviorKey.Idle.Value, actor.CurrentKey.Value);
        }

        /// <summary>移動意図で Locomotion へ遷移し、Pose が進み、速度が Avatar に届く。</summary>
        [Test]
        public void MoveIntent_TransitionsToLocomotion_AndMovesPose()
        {
            var avatar = new FakeAvatar();
            var actor = BuildActor(avatar);

            TickWith(actor, new CharacterIntent(Vector3.forward, false, BehaviorKey.None, 0), 0.5f);

            Assert.AreEqual(BehaviorKey.Locomotion.Value, actor.CurrentKey.Value);
            Assert.Greater(actor.Pose.Position.z, 0f, "前方へ進んでいる");
            Assert.AreEqual(1f, avatar.LastSpeed, 0.001f, "全力の移動速度が Avatar へ届く");
        }

        /// <summary>移動をやめると Idle へ戻る。</summary>
        [Test]
        public void StopMoving_ReturnsToIdle()
        {
            var actor = BuildActor(new FakeAvatar());
            TickWith(actor, new CharacterIntent(Vector3.forward, false, BehaviorKey.None, 0));
            TickWith(actor, CharacterIntent.None);
            Assert.AreEqual(BehaviorKey.Idle.Value, actor.CurrentKey.Value);
        }

        /// <summary>攻撃要求で Attack へ遷移し、持続時間の満了で Idle へ戻る。</summary>
        [Test]
        public void AttackIntent_RunsForDuration_ThenReturnsToIdle()
        {
            var avatar = new FakeAvatar();
            var actor = BuildActor(avatar, attackSeconds: 0.4f);

            TickWith(actor, new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 0));
            Assert.AreEqual(BehaviorKey.Attack.Value, actor.CurrentKey.Value);
            Assert.IsTrue(avatar.LastTransitionTo(BehaviorKey.Attack));

            TickWith(actor, CharacterIntent.None, 0.2f); // 経過 0.1+0.2 = 0.3 < 0.4
            Assert.AreEqual(BehaviorKey.Attack.Value, actor.CurrentKey.Value, "振りの途中");

            TickWith(actor, CharacterIntent.None, 0.2f); // 経過 0.5 >= 0.4 → 完了
            // 遷移解決は「Tickの先頭」で行う設計のため、完了の反映は次のTick
            TickWith(actor, CharacterIntent.None, 0.01f);
            Assert.AreEqual(BehaviorKey.Idle.Value, actor.CurrentKey.Value, "振り切って待機へ");
        }

        /// <summary>攻撃中のガード要求は優先度で拒否される（ガードで攻撃をキャンセルできない）。</summary>
        [Test]
        public void GuardDuringAttack_IsRejected()
        {
            var actor = BuildActor(new FakeAvatar());
            TickWith(actor, new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 0));

            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.None, 0));
            Assert.AreEqual(BehaviorKey.Attack.Value, actor.CurrentKey.Value, "振り中はガードに移れない");
        }

        /// <summary>ガード中の攻撃要求は無視される（ガード優先の意図規則）。</summary>
        [Test]
        public void AttackDuringGuard_IsIgnored()
        {
            var actor = BuildActor(new FakeAvatar());
            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.None, 0));
            Assert.AreEqual(BehaviorKey.Guard.Value, actor.CurrentKey.Value);

            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.Attack, 0));
            Assert.AreEqual(BehaviorKey.Guard.Value, actor.CurrentKey.Value, "構え中は攻撃を出さない");
        }

        /// <summary>被弾（Hit）は攻撃を割り込み、のけぞり後に Idle へ戻る。</summary>
        [Test]
        public void Hit_InterruptsAttack_ThenRecoversToIdle()
        {
            var avatar = new FakeAvatar();
            var actor = BuildActor(avatar, staggerSeconds: 0.25f);
            TickWith(actor, new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, 0));

            Assert.IsTrue(actor.RequestBehavior(BehaviorKey.Hit), "被弾は攻撃を割り込める");
            Assert.AreEqual(BehaviorKey.Hit.Value, actor.CurrentKey.Value);

            TickWith(actor, CharacterIntent.None, 0.3f);  // のけぞり満了
            TickWith(actor, CharacterIntent.None, 0.01f); // 完了の反映は次のTick
            Assert.AreEqual(BehaviorKey.Idle.Value, actor.CurrentKey.Value);
        }

        /// <summary>のけぞり満了時にガード意図が続いていれば Guard へ戻る。</summary>
        [Test]
        public void HitRecovery_WithGuardHeld_ReturnsToGuard()
        {
            var actor = BuildActor(new FakeAvatar());
            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.None, 0));
            actor.RequestBehavior(BehaviorKey.Hit);

            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.None, 0), 0.3f);  // 満了
            TickWith(actor, new CharacterIntent(Vector3.zero, true, BehaviorKey.None, 0), 0.01f); // 反映
            Assert.AreEqual(BehaviorKey.Guard.Value, actor.CurrentKey.Value, "構えへ復帰する");
        }

        /// <summary>Death は終端: force 以外のあらゆる要求・意図で抜けられない。</summary>
        [Test]
        public void Death_IsTerminal()
        {
            var actor = BuildActor(new FakeAvatar());
            actor.SetAlive(false);
            actor.RequestBehavior(BehaviorKey.Death, 0, force: true);

            Assert.IsFalse(actor.RequestBehavior(BehaviorKey.Hit), "被弾でも起き上がらない");
            TickWith(actor, new CharacterIntent(Vector3.forward, true, BehaviorKey.Attack, 0));
            Assert.AreEqual(BehaviorKey.Death.Value, actor.CurrentKey.Value, "意図でも抜けられない");
        }

        /// <summary>同一行動の重複登録は構成ミスとして例外。</summary>
        [Test]
        public void DuplicateBehavior_Throws()
        {
            var actor = new CharacterActor(new ActorKey(1), new FakeAvatar());
            actor.AddBehavior(new IdleBehavior());
            Assert.Throws<Seed.Hub.HubException>(() => actor.AddBehavior(new IdleBehavior()));
        }

        /// <summary>未登録行動の要求は打ち間違いとして例外。</summary>
        [Test]
        public void UnknownBehaviorRequest_Throws()
        {
            var actor = new CharacterActor(new ActorKey(1), new FakeAvatar());
            actor.AddBehavior(new IdleBehavior());
            actor.Activate(BehaviorKey.Idle);
            Assert.Throws<Seed.Hub.HubException>(() => actor.RequestBehavior(new BehaviorKey(999)));
        }

        /// <summary>移動は MotionSolver 経由で解決される（物理・NavMesh統合の差し込み口）。</summary>
        [Test]
        public void Locomotion_DelegatesToMotionSolver()
        {
            var actor = BuildActor(new FakeAvatar());
            actor.MotionSolver = new HalvingSolver(); // 希望変位を半分に切り詰める疑似衝突

            TickWith(actor, new CharacterIntent(Vector3.forward, false, BehaviorKey.None, 0), 1f);

            // 素通しなら 4m/s×1s=4m 進むが、ソルバーが半分に切り詰める
            Assert.AreEqual(2f, actor.Pose.Position.z, 0.001f, "到達位置はソルバーの真実");
        }

        /// <summary>希望変位を半分にする疑似ソルバー（壁ずり等の代役）。</summary>
        private sealed class HalvingSolver : IMotionSolver
        {
            /// <summary>半分だけ動けたことにする。</summary>
            public Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta)
            {
                return currentPosition + desiredDelta * 0.5f;
            }
        }
    }
}
