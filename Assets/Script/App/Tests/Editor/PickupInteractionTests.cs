using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Seed.App.Tests
{
    /// <summary>「物を拾う」インタラクション（腕IKデモの状態機械）のテスト。</summary>
    public sealed class PickupInteractionTests
    {
        /// <summary>1歩ぶんの刻み（60fps相当）。</summary>
        private const float Step = 1f / 60f;

        /// <summary>テスト中に作った GameObject（後始末用）。</summary>
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>作ったオブジェクトを毎テスト後に破棄する。</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
        }

        /// <summary>範囲外のアイテムへは開始できない。</summary>
        [Test]
        public void 範囲外では開始できない()
        {
            var interaction = new Sample_PickupInteraction(startRange: 1.6f);
            interaction.AddItem(CreateTransform(new Vector3(5f, 0.85f, 0f)));

            Assert.That(interaction.TryStart(Vector3.zero), Is.False);
            Assert.That(interaction.IsBusy, Is.False);
            Assert.That(interaction.Weight, Is.EqualTo(0f));
        }

        /// <summary>伸ばす→掴む→戻すの一連で拾い切り、アイテムは手に吸着して消える。</summary>
        [Test]
        public void 一連の流れで拾い切る()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(new Vector3(0.3f, 1.1f, 0f));
            interaction.AddItem(item);

            Assert.That(interaction.TryStart(Vector3.zero), Is.True);

            var sawFullWeight = false;
            for (var i = 0; i < 600 && interaction.IsBusy; i++)
            {
                interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);
                sawFullWeight |= interaction.Weight > 0.99f;
            }

            Assert.That(interaction.IsBusy, Is.False, "有限時間で完了する");
            Assert.That(sawFullWeight, Is.True, "腕は一度伸ばし切る");
            Assert.That(interaction.PickedCount, Is.EqualTo(1));
            Assert.That(interaction.Weight, Is.EqualTo(0f), "腕は戻り切る");
            Assert.That(item.gameObject.activeSelf, Is.False, "拾ったアイテムは消える");
            Assert.That(item.parent, Is.EqualTo(hand), "手に吸着したまま消えている");
            Assert.That(item.localPosition.magnitude, Is.LessThan(0.01f), "手の中へ引き寄せられて消える");
            Assert.That(interaction.Items, Has.No.Member(item));
        }

        /// <summary>伸ばしている途中に走り出すと中断し、アイテムは拾われず残る。</summary>
        [Test]
        public void 走り出すと中断してアイテムは残る()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(Vector3.zero);
            interaction.AddItem(item);
            interaction.TryStart(Vector3.zero);

            // 少し伸ばしてから走り出す
            for (var i = 0; i < 8; i++)
            {
                interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);
            }
            Assert.That(interaction.Weight, Is.GreaterThan(0f));
            for (var i = 0; i < 600 && interaction.IsBusy; i++)
            {
                interaction.Tick(Step, Vector3.zero, planarSpeed: 4f, hand);
            }

            Assert.That(interaction.PickedCount, Is.EqualTo(0));
            Assert.That(interaction.Weight, Is.EqualTo(0f), "腕は滑らかに戻り切る");
            Assert.That(item.gameObject.activeSelf, Is.True, "アイテムは残る");
            Assert.That(interaction.Items, Has.Member(item));
            Assert.That(interaction.TryStart(Vector3.zero), Is.True, "もう一度拾い直せる");
        }

        /// <summary>どの段階でも重みは 0〜1 を外れない（IKへ渡す値の安全域）。</summary>
        [Test]
        public void 重みは常に0から1に収まる()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(Vector3.zero);
            interaction.AddItem(item);
            interaction.TryStart(Vector3.zero);

            for (var i = 0; i < 600 && interaction.IsBusy; i++)
            {
                interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);
                Assert.That(interaction.Weight, Is.InRange(0f, 1f));
            }
        }

        /// <summary>走りながらの開始は即座に解け、無反応なロック時間を作らない。</summary>
        [Test]
        public void 走りながらの開始は即座に解けてロックしない()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(Vector3.zero);
            interaction.AddItem(item);

            Assert.That(interaction.TryStart(Vector3.zero), Is.True);
            interaction.Tick(Step, Vector3.zero, planarSpeed: 4f, hand); // 走行中＝即中断

            Assert.That(interaction.IsBusy, Is.False, "重み0からの中断は即待機（0.35秒の空白が無い）");
            Assert.That(interaction.Weight, Is.EqualTo(0f));
            Assert.That(interaction.TryStart(Vector3.zero), Is.True, "すぐ拾い直せる");
        }

        /// <summary>決着などの外部事情では即座に畳め、掴みかけは拾い切った扱いになる。</summary>
        [Test]
        public void 外部事情で即座に畳める()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(Vector3.zero);
            interaction.AddItem(item);
            interaction.TryStart(Vector3.zero);

            // 掴み段階（重み1到達後）まで進めてから畳む
            for (var i = 0; i < 40 && interaction.Weight < 1f; i++)
            {
                interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);
            }
            interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);
            interaction.ForceFinish();

            Assert.That(interaction.IsBusy, Is.False);
            Assert.That(interaction.Weight, Is.EqualTo(0f));
            Assert.That(interaction.PickedCount, Is.EqualTo(1), "掴みかけは拾い切った扱い");
            Assert.That(item.gameObject.activeSelf, Is.False, "半端な大きさで残らない");
        }

        /// <summary>原点回帰でワールドがずれたら目標点も追従する（腕が旧座標へ跳ねない）。</summary>
        [Test]
        public void 原点回帰で目標点が追従する()
        {
            var interaction = new Sample_PickupInteraction();
            var item = CreateTransform(new Vector3(1f, 0.85f, 0f));
            var hand = CreateTransform(Vector3.zero);
            interaction.AddItem(item);
            interaction.TryStart(Vector3.zero);
            interaction.Tick(Step, Vector3.zero, planarSpeed: 0f, hand);

            var before = interaction.TargetPoint;
            var delta = new Vector3(-90f, 0f, -90f);
            interaction.ShiftOrigin(delta);

            Assert.That(interaction.TargetPoint, Is.EqualTo(before + delta));
        }

        /// <summary>位置つきの Transform を作る（後始末リストへ登録）。</summary>
        private Transform CreateTransform(Vector3 position)
        {
            var go = new GameObject("PickupTestObject");
            go.transform.position = position;
            _spawned.Add(go);
            return go.transform;
        }
    }
}
