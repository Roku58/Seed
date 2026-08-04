using NUnit.Framework;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>ManualLogic（手動操作の意図生成）のテスト。</summary>
    public sealed class ManualLogicTests
    {
        /// <summary>空の LogicFrame を作る補助。</summary>
        private static LogicFrame Frame(float dt = 0.1f)
        {
            return new LogicFrame(new CharacterId(1), Vector3.zero, Quaternion.identity, true, dt);
        }

        /// <summary>入力なしなら空の意図。</summary>
        [Test]
        public void NoInput_ProducesNoneIntent()
        {
            var logic = new ManualLogic();
            var intent = logic.Think(Frame());

            Assert.AreEqual(Vector3.zero, intent.MoveDirection);
            Assert.IsFalse(intent.GuardHeld);
            Assert.AreEqual(BehaviorKey.None.Value, intent.RequestedAction.Value);
        }

        /// <summary>移動入力はそのまま意図に乗る（毎フレーム上書き）。</summary>
        [Test]
        public void SetMove_CarriesDirection()
        {
            var logic = new ManualLogic();
            logic.SetMove(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(new Vector3(1f, 0f, 0f), logic.Think(Frame()).MoveDirection);

            logic.SetMove(Vector3.zero);
            Assert.AreEqual(Vector3.zero, logic.Think(Frame()).MoveDirection, "上書きされる");
        }

        /// <summary>斜め入力（大きさ>1）は正規化される。</summary>
        [Test]
        public void SetMove_NormalizesOverUnitVector()
        {
            var logic = new ManualLogic();
            logic.SetMove(new Vector3(1f, 0f, 1f)); // 大きさ √2
            var move = logic.Think(Frame()).MoveDirection;
            Assert.AreEqual(1f, move.magnitude, 0.001f, "斜めでも速度が増えない");
        }

        /// <summary>上下成分（y）は落とされる。</summary>
        [Test]
        public void SetMove_DropsVerticalComponent()
        {
            var logic = new ManualLogic();
            logic.SetMove(new Vector3(0f, 5f, 0.5f));
            var move = logic.Think(Frame()).MoveDirection;
            Assert.AreEqual(0f, move.y);
            Assert.AreEqual(0.5f, move.z, 0.001f);
        }

        /// <summary>ガードは継続入力としてそのまま乗る。</summary>
        [Test]
        public void SetGuard_CarriesHeldState()
        {
            var logic = new ManualLogic();
            logic.SetGuard(true);
            Assert.IsTrue(logic.Think(Frame()).GuardHeld);
            Assert.IsTrue(logic.Think(Frame()).GuardHeld, "継続入力は消費されない");

            logic.SetGuard(false);
            Assert.IsFalse(logic.Think(Frame()).GuardHeld);
        }

        /// <summary>行動要求は一度だけ意図に乗る（押しっぱなしでも1回）。</summary>
        [Test]
        public void RequestAction_IsConsumedOnce()
        {
            var logic = new ManualLogic();
            logic.RequestAction(BehaviorKey.Attack, 42);

            var first = logic.Think(Frame());
            Assert.AreEqual(BehaviorKey.Attack.Value, first.RequestedAction.Value);
            Assert.AreEqual(42, first.ActionPayload);

            var second = logic.Think(Frame());
            Assert.AreEqual(BehaviorKey.None.Value, second.RequestedAction.Value, "消費済み");
            Assert.AreEqual(0, second.ActionPayload);
        }

        /// <summary>未消費のうちの再予約は後勝ち。</summary>
        [Test]
        public void RequestAction_LatestWins()
        {
            var logic = new ManualLogic();
            logic.RequestAction(BehaviorKey.Attack, 1);
            logic.RequestAction(BehaviorKey.Attack, 2);

            Assert.AreEqual(2, logic.Think(Frame()).ActionPayload);
        }
    }
}
