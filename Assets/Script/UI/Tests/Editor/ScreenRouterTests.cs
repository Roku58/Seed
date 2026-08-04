using System.Collections.Generic;
using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.UI.Tests
{
    /// <summary>ScreenRouter（レイヤ・履歴・遷移モード・出入り演出）のテスト。</summary>
    public sealed class ScreenRouterTests
    {
        /// <summary>テスト用画面（ID・レイヤ・演出を差し込める）。</summary>
        private sealed class TestScreen : UIScreen
        {
            /// <summary>この画面のID（テストが設定）。</summary>
            public ScreenId IdValue;

            /// <summary>この画面のレイヤ（テストが設定）。</summary>
            public ScreenLayer LayerValue = ScreenLayer.Base;

            /// <summary>抜け演出（テストが設定。null なら即時）。</summary>
            public FakeTransition HideTransition;

            /// <summary>OnShow で受けた荷物の記録。</summary>
            public readonly List<int> ShownPayloads = new List<int>();

            /// <summary>OnHide の回数。</summary>
            public int HideCount;

            /// <summary>この画面のID。</summary>
            public override ScreenId Id => IdValue;

            /// <summary>この画面のレイヤ。</summary>
            public override ScreenLayer Layer => LayerValue;

            /// <summary>表示を記録する。</summary>
            protected override void OnShow()
            {
                ShownPayloads.Add(Payload);
            }

            /// <summary>非表示を記録する。</summary>
            protected override void OnHide()
            {
                HideCount++;
            }

            /// <summary>設定された抜け演出を返す。</summary>
            protected override IScreenTransition CreateHideTransition()
            {
                return HideTransition;
            }
        }

        /// <summary>指定Tick数で完了する偽の演出。</summary>
        public sealed class FakeTransition : IScreenTransition
        {
            /// <summary>完了までの残りTick数。</summary>
            private int _remainingTicks;

            /// <summary>Complete が呼ばれたか。</summary>
            public bool WasCompleted;

            /// <summary>FakeTransition を生成する。</summary>
            public FakeTransition(int ticks)
            {
                _remainingTicks = ticks;
            }

            /// <summary>完了したか。</summary>
            public bool IsDone => _remainingTicks <= 0;

            /// <summary>1Tick分消化する。</summary>
            public void Tick(float deltaTime)
            {
                _remainingTicks--;
            }

            /// <summary>終端へ飛ばす。</summary>
            public void Complete()
            {
                _remainingTicks = 0;
                WasCompleted = true;
            }
        }

        /// <summary>生成したGameObject（後始末用）。</summary>
        private readonly List<GameObject> _created = new List<GameObject>();

        /// <summary>GameObjectを片付ける。</summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        /// <summary>テスト用画面を作る。</summary>
        private TestScreen CreateScreen(int id, ScreenLayer layer = ScreenLayer.Base)
        {
            var go = new GameObject($"Screen{id}");
            _created.Add(go);
            var screen = go.AddComponent<TestScreen>();
            screen.IdValue = new ScreenId(id);
            screen.LayerValue = layer;
            return screen;
        }

        /// <summary>二重登録は構成ミスとして例外。未登録の表示も例外。</summary>
        [Test]
        public void Register_DuplicateAndUnknown_Throw()
        {
            var router = new ScreenRouter();
            router.Register(CreateScreen(1));
            Assert.Throws<HubException>(() => router.Register(CreateScreen(1)));
            Assert.Throws<HubException>(() => router.Show(new ScreenId(9)));
        }

        /// <summary>Push で履歴が積まれ、Back で荷物なしに戻る。</summary>
        [Test]
        public void Push_ThenBack_RestoresPrevious()
        {
            var router = new ScreenRouter();
            var hud = CreateScreen(1);
            var menu = CreateScreen(2);
            router.Register(hud);
            router.Register(menu);

            router.Show(new ScreenId(1), ScreenTransition.Push, payload: 42);
            Assert.AreEqual(42, hud.ShownPayloads[0], "荷物が届く");

            router.Show(new ScreenId(2));
            Assert.IsFalse(hud.gameObject.activeSelf, "前の画面は消える");
            Assert.AreEqual(1, router.HistoryCount);

            Assert.IsTrue(router.Back());
            Assert.AreEqual(1, router.Current.Value);
            Assert.IsTrue(hud.gameObject.activeSelf);
            Assert.IsFalse(router.Back(), "履歴が尽きたら false");
        }

        /// <summary>Replace は履歴に積まず、ClearAndShow は履歴を消す。</summary>
        [Test]
        public void ReplaceAndClearAndShow_ControlHistory()
        {
            var router = new ScreenRouter();
            router.Register(CreateScreen(1));
            router.Register(CreateScreen(2));
            router.Register(CreateScreen(3));

            router.Show(new ScreenId(1));
            router.Show(new ScreenId(2), ScreenTransition.Replace);
            Assert.AreEqual(0, router.HistoryCount, "Replace は積まない");

            router.Show(new ScreenId(3), ScreenTransition.Push);
            Assert.AreEqual(1, router.HistoryCount);
            router.Show(new ScreenId(1), ScreenTransition.ClearAndShow);
            Assert.AreEqual(0, router.HistoryCount, "ClearAndShow は全消去（タイトルへ戻る等）");
        }

        /// <summary>履歴は上限で頭打ちになる（往復の無限成長防止）。</summary>
        [Test]
        public void History_IsCapped()
        {
            var router = new ScreenRouter();
            router.Register(CreateScreen(1));
            router.Register(CreateScreen(2));
            for (var i = 0; i < 40; i++)
            {
                router.Show(new ScreenId(1 + (i % 2)));
            }
            Assert.LessOrEqual(router.HistoryCount, ScreenRouter.MaxHistory);
        }

        /// <summary>Modal はBaseを消さずに重なり、後入れ先出しで閉じる。</summary>
        [Test]
        public void Modal_StacksOverBase_AndClosesLifo()
        {
            var router = new ScreenRouter();
            var hud = CreateScreen(1);
            var pause = CreateScreen(2, ScreenLayer.Modal);
            var confirm = CreateScreen(3, ScreenLayer.Modal);
            router.Register(hud);
            router.Register(pause);
            router.Register(confirm);

            router.Show(new ScreenId(1));
            router.Show(new ScreenId(2));
            router.Show(new ScreenId(3));

            Assert.IsTrue(hud.gameObject.activeSelf, "Base は消えない");
            Assert.AreEqual(2, router.ModalCount);

            Assert.IsTrue(router.Close(ScreenLayer.Modal));
            Assert.IsFalse(confirm.gameObject.activeSelf, "後入れが先に閉じる");
            Assert.IsTrue(pause.gameObject.activeSelf);
            Assert.IsTrue(router.Close(ScreenLayer.Modal));
            Assert.IsFalse(router.Close(ScreenLayer.Modal), "空なら false");
        }

        /// <summary>抜け演出中は表示が残り、完了で消える（クロスフェードの土台）。</summary>
        [Test]
        public void HideTransition_KeepsVisibleUntilDone()
        {
            var router = new ScreenRouter();
            var first = CreateScreen(1);
            var second = CreateScreen(2);
            first.HideTransition = new FakeTransition(ticks: 2);
            router.Register(first);
            router.Register(second);
            router.Show(new ScreenId(1));

            router.Show(new ScreenId(2));

            Assert.AreEqual(1, first.HideCount, "状態変更（購読解除）は即時");
            Assert.IsTrue(first.gameObject.activeSelf, "演出中は表示が残る");
            Assert.IsTrue(second.gameObject.activeSelf, "次の画面はもう出ている＝クロスフェード");
            Assert.IsTrue(router.IsTransitionRunning);

            router.Tick(0.016f);
            Assert.IsTrue(first.gameObject.activeSelf, "まだ演出中");
            router.Tick(0.016f);
            Assert.IsFalse(first.gameObject.activeSelf, "演出完了で消える");
            Assert.IsFalse(router.IsTransitionRunning);
        }

        /// <summary>抜け演出中の再表示は、演出を終端へ飛ばしてから出直す（状態が濁らない）。</summary>
        [Test]
        public void ReShowDuringHideTransition_CompletesItFirst()
        {
            var router = new ScreenRouter();
            var first = CreateScreen(1);
            var second = CreateScreen(2);
            var transition = new FakeTransition(ticks: 100);
            first.HideTransition = transition;
            router.Register(first);
            router.Register(second);

            router.Show(new ScreenId(1));
            router.Show(new ScreenId(2)); // first は長い抜け演出へ
            router.Back();                // 演出が終わる前に first へ戻る

            Assert.IsTrue(transition.WasCompleted, "進行中の演出は終端へ飛ばされる");
            Assert.IsTrue(first.gameObject.activeSelf, "出直して表示されている");
            Assert.AreEqual(2, first.ShownPayloads.Count, "OnShow は2回目");
        }
    }
}
