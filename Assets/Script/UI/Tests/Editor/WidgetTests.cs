using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Seed.UI.Tests
{
    /// <summary>UI部品ラッパー（SeedButton / SeedToggle / SeedSlider）のテスト。</summary>
    public sealed class WidgetTests
    {
        /// <summary>テスト中に作った GameObject（確実に片付ける）。</summary>
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>偽の現在時刻（クールダウンを待ち時間なしで検証する）。</summary>
        private float _now;

        /// <summary>EditMode では即時破棄で片付ける。</summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();
        }

        /// <summary>ボタンを組む（時刻は偽物を注入）。</summary>
        private SeedButton CreateButton()
        {
            var go = new GameObject("Button");
            _spawned.Add(go);
            var button = go.AddComponent<SeedButton>();
            button.TimeSource = () => _now;
            return button;
        }

        /// <summary>左クリックのイベントを作る。</summary>
        private static PointerEventData LeftClick()
        {
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
        }

        // ================================================================
        // SeedButton
        // ================================================================

        /// <summary>購読が発火し、Dispose で解除される（解除漏れ防止の本命機能）。</summary>
        [Test]
        public void Button_OnClick_FiresAndDisposes()
        {
            var button = CreateButton();
            var count = 0;
            var token = button.OnClick(() => count++);

            button.OnPointerClick(LeftClick());
            Assert.AreEqual(1, count, "クリックで発火");

            token.Dispose();
            _now += 10f; // クールダウンの影響を除く
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(1, count, "解除後は発火しない");
        }

        /// <summary>クールダウン中の連打は1回に吸収される（二重実行事故の防止）。</summary>
        [Test]
        public void Button_Cooldown_AbsorbsRapidClicks()
        {
            var button = CreateButton();
            button.CooldownSeconds = 0.15f;
            var count = 0;
            button.OnClick(() => count++);

            button.OnPointerClick(LeftClick());
            button.OnPointerClick(LeftClick()); // 同一時刻の連打
            Assert.AreEqual(1, count, "2連打は1回");

            _now += 0.2f; // クールダウン明け
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(2, count, "時間が経てば受け付ける");
        }

        /// <summary>処理中ロック（BeginBusy）中は押せず、解除で戻る。</summary>
        [Test]
        public void Button_BeginBusy_BlocksUntilDisposed()
        {
            var button = CreateButton();
            button.CooldownSeconds = 0f;
            var count = 0;
            button.OnClick(() => count++);

            var busy = button.BeginBusy();
            Assert.IsTrue(button.IsBusy);
            Assert.IsFalse(button.interactable, "見た目も無効化される");
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(0, count, "処理中は押せない");

            busy.Dispose();
            Assert.IsFalse(button.IsBusy);
            Assert.IsTrue(button.interactable, "解除で元の interactable へ戻る");
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(1, count, "解除後は押せる");
        }

        /// <summary>ロックの重なり（2重 BeginBusy）は最後の解除まで押せない。</summary>
        [Test]
        public void Button_NestedBusy_UnlocksAtLastDispose()
        {
            var button = CreateButton();
            button.CooldownSeconds = 0f;
            var count = 0;
            button.OnClick(() => count++);

            var a = button.BeginBusy();
            var b = button.BeginBusy();
            a.Dispose();
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(0, count, "片方の解除ではまだロック中");

            b.Dispose();
            button.OnPointerClick(LeftClick());
            Assert.AreEqual(1, count, "全部解除で受け付ける");
        }

        /// <summary>interactable=false では発火しない（uGUI の既定と揃える）。</summary>
        [Test]
        public void Button_NotInteractable_DoesNotFire()
        {
            var button = CreateButton();
            var count = 0;
            button.OnClick(() => count++);
            button.interactable = false;

            button.OnPointerClick(LeftClick());
            Assert.AreEqual(0, count);
        }

        /// <summary>右クリックはボタンのクリックとして扱わない。</summary>
        [Test]
        public void Button_RightClick_IsIgnored()
        {
            var button = CreateButton();
            var count = 0;
            button.OnClick(() => count++);

            button.OnPointerClick(new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right,
            });
            Assert.AreEqual(0, count);
        }

        /// <summary>操作の共通フック（SE の一元化ポイント）が発火する。</summary>
        [Test]
        public void Button_Click_RaisesInteractedHook()
        {
            var button = CreateButton();
            Component received = null;
            void Hook(Component c) => received = c;
            SeedUiEffects.Interacted += Hook;
            try
            {
                button.OnPointerClick(LeftClick());
                Assert.AreSame(button, received, "操作元が通知される");
            }
            finally
            {
                SeedUiEffects.Interacted -= Hook;
            }
        }

        // ================================================================
        // 長押し（純C#の判定）
        // ================================================================

        /// <summary>閾値に達した瞬間に1回だけ成立する。</summary>
        [Test]
        public void LongPress_FiresOncePerHold()
        {
            var tracker = new LongPressTracker();
            tracker.Begin(now: 10f);

            Assert.IsFalse(tracker.TryFire(10.3f, thresholdSeconds: 0.5f), "まだ足りない");
            Assert.IsTrue(tracker.TryFire(10.5f, 0.5f), "閾値で成立");
            Assert.IsTrue(tracker.Fired);
            Assert.IsFalse(tracker.TryFire(11f, 0.5f), "同じ押下では2回目は無い");

            tracker.Begin(11.5f);
            Assert.IsTrue(tracker.TryFire(12f, 0.5f), "押し直せばまた成立する");
        }

        /// <summary>閾値0は無効・途中で離せば成立しない。</summary>
        [Test]
        public void LongPress_DisabledAndCancel()
        {
            var tracker = new LongPressTracker();
            tracker.Begin(0f);
            Assert.IsFalse(tracker.TryFire(100f, thresholdSeconds: 0f), "0秒指定は無効");

            tracker.Begin(0f);
            tracker.Cancel(); // 途中で離した
            Assert.IsFalse(tracker.TryFire(10f, 0.5f), "離した後は成立しない");
        }

        // ================================================================
        // SeedToggle / SeedSlider
        // ================================================================

        /// <summary>トグルの購読と解除。SetIsOnWithoutNotify は購読者を呼ばない。</summary>
        [Test]
        public void Toggle_Subscription_AndSilentSet()
        {
            var go = new GameObject("Toggle");
            _spawned.Add(go);
            var toggle = go.AddComponent<SeedToggle>();
            var received = new List<bool>();
            var token = toggle.OnValueChanged(v => received.Add(v));

            toggle.isOn = true;
            CollectionAssert.AreEqual(new[] { true }, received, "操作は届く");

            toggle.SetIsOnWithoutNotify(false);
            Assert.AreEqual(1, received.Count, "表示合わせは届かない（事故防止の使い分け）");

            token.Dispose();
            toggle.isOn = true;
            Assert.AreEqual(1, received.Count, "解除後は届かない");
        }

        /// <summary>スライダーの刻み: 全経路（value 代入・SetValueWithoutNotify）で吸着する。</summary>
        [Test]
        public void Slider_Step_SnapsAllPaths()
        {
            var go = new GameObject("Slider");
            _spawned.Add(go);
            var slider = go.AddComponent<SeedSlider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.Step = 0.25f;

            slider.value = 0.4f;
            Assert.AreEqual(0.5f, slider.value, 0.0001f, "0.4 → 0.5 へ吸着");

            slider.SetValueWithoutNotify(0.1f);
            Assert.AreEqual(0f, slider.value, 0.0001f, "通知なし経路でも吸着");

            slider.Step = 0f;
            slider.value = 0.4f;
            Assert.AreEqual(0.4f, slider.value, 0.0001f, "刻み0は自由値");
        }

        /// <summary>スライダーの購読と解除。</summary>
        [Test]
        public void Slider_Subscription_Disposes()
        {
            var go = new GameObject("Slider");
            _spawned.Add(go);
            var slider = go.AddComponent<SeedSlider>();
            slider.minValue = 0f;
            slider.maxValue = 10f;
            var count = 0;
            var token = slider.OnValueChanged(_ => count++);

            slider.value = 3f;
            Assert.AreEqual(1, count);

            token.Dispose();
            slider.value = 7f;
            Assert.AreEqual(1, count, "解除後は届かない");
        }
    }
}
