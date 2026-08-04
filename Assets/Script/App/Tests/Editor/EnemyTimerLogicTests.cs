using NUnit.Framework;
using Seed.Character;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App.Tests
{
    /// <summary>Sample_EnemyTimerLogic（間隔攻撃AI）のテスト。</summary>
    public sealed class EnemyTimerLogicTests
    {
        /// <summary>指定dtの LogicFrame を作る補助。</summary>
        private static LogicFrame Frame(float dt)
        {
            return new LogicFrame(new CharacterId(2), Vector3.zero, Quaternion.identity, true, dt);
        }

        // dt は 0.5f（二進で正確な値）を使う——0.1f の40回加算は 3.9999998 になり
        // 「ちょうど4.0秒」を踏まない。浮動小数の性質でありロジックの欠陥ではない。

        /// <summary>間隔未満では攻撃意図を出さない。</summary>
        [Test]
        public void BeforeInterval_NoAttackIntent()
        {
            var logic = new Sample_EnemyTimerLogic(4f, moveId: 102);
            for (var i = 0; i < 7; i++) // 3.5秒
            {
                var intent = logic.Think(Frame(0.5f));
                Assert.AreEqual(BehaviorKey.None.Value, intent.RequestedAction.Value, $"tick {i}");
            }
        }

        /// <summary>間隔到達フレームで1回だけ攻撃意図が立ち、技IDが荷物に乗る。</summary>
        [Test]
        public void AtInterval_FiresOnce_WithMoveId()
        {
            var logic = new Sample_EnemyTimerLogic(4f, moveId: 102);
            var fired = 0;
            for (var i = 0; i < 8; i++) // ちょうど4.0秒
            {
                var intent = logic.Think(Frame(0.5f));
                if (!intent.RequestedAction.Equals(BehaviorKey.None))
                {
                    fired++;
                    Assert.AreEqual(BehaviorKey.Attack.Value, intent.RequestedAction.Value);
                    Assert.AreEqual(102, intent.ActionPayload, "技IDが素通しで乗る");
                }
            }
            Assert.AreEqual(1, fired, "発火は1回だけ");
        }

        /// <summary>発火後にタイマーが戻り、次の間隔で再発火する（細切れTickでも周期が保たれる）。</summary>
        [Test]
        public void AfterFire_TimerResets_AndFiresAgain()
        {
            var logic = new Sample_EnemyTimerLogic(4f, moveId: 102);
            var fired = 0;
            for (var i = 0; i < 24; i++) // 12秒 → 3回
            {
                if (!logic.Think(Frame(0.5f)).RequestedAction.Equals(BehaviorKey.None))
                {
                    fired++;
                }
            }
            Assert.AreEqual(3, fired);
        }
    }
}
