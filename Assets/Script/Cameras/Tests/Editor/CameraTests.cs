using NUnit.Framework;
using Seed.Hub.Contracts;

namespace Seed.Cameras.Tests
{
    /// <summary>視点の重ね合わせ台（どの視点が有効かの判断）のテスト。純C#。</summary>
    public sealed class CameraTests
    {
        /// <summary>初期状態は視点なし。</summary>
        [Test]
        public void Stack_StartsEmpty()
        {
            var stack = new ViewpointStack();
            Assert.AreEqual(ViewpointId.None, stack.Active, "初期は視点なし");
            Assert.AreEqual(0, stack.OverlayCount, "重ねも無い");
        }

        /// <summary>基本の視点を切り替えると有効な視点も変わる（FPS ⇄ TPS）。</summary>
        [Test]
        public void SetBase_SwitchesActive()
        {
            var stack = new ViewpointStack();
            Assert.IsTrue(stack.SetBase(ViewpointId.ThirdPerson), "変化したので true");
            Assert.AreEqual(ViewpointId.ThirdPerson, stack.Active);

            Assert.IsTrue(stack.SetBase(ViewpointId.FirstPerson), "一人称へ切替");
            Assert.AreEqual(ViewpointId.FirstPerson, stack.Active);

            Assert.IsFalse(stack.SetBase(ViewpointId.FirstPerson), "同じ視点は変化なし");
        }

        /// <summary>演出視点は上に重なり、取り下げると元の基本へ戻る。</summary>
        [Test]
        public void Overlay_CoversBase_AndRestoresOnPop()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);

            Assert.IsTrue(stack.Push(ViewpointId.Cutscene), "重ねると有効視点が変わる");
            Assert.AreEqual(ViewpointId.Cutscene, stack.Active);
            Assert.AreEqual(1, stack.OverlayCount);

            Assert.IsTrue(stack.Pop(ViewpointId.Cutscene), "取り下げで元へ");
            Assert.AreEqual(ViewpointId.ThirdPerson, stack.Active, "基本の視点へ必ず戻る");
            Assert.AreEqual(0, stack.OverlayCount);
        }

        /// <summary>重ね中に基本を切り替えても見え方は変わらず、取り下げ後に反映される。</summary>
        [Test]
        public void SetBase_UnderOverlay_AppliesAfterPop()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            stack.Push(ViewpointId.Cutscene);

            Assert.IsFalse(stack.SetBase(ViewpointId.FirstPerson),
                "重ねに隠れているので今の見え方は変わらない");
            Assert.AreEqual(ViewpointId.Cutscene, stack.Active);

            Assert.IsTrue(stack.Pop(ViewpointId.Cutscene));
            Assert.AreEqual(ViewpointId.FirstPerson, stack.Active, "取り下げ後に新しい基本が出る");
        }

        /// <summary>同じ視点を二重に積まない（最前面へ移すだけ）。</summary>
        [Test]
        public void Push_SameViewpointTwice_MovesToFront()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            stack.Push(ViewpointId.Cutscene);
            stack.Push(ViewpointId.Overhead);

            Assert.IsTrue(stack.Push(ViewpointId.Cutscene), "最前面へ移動して有効視点が変わる");
            Assert.AreEqual(2, stack.OverlayCount, "枚数は増えない");
            Assert.AreEqual(ViewpointId.Cutscene, stack.Active);
        }

        /// <summary>取り下げは順不同（演出が入れ違って終わっても破綻しない）。</summary>
        [Test]
        public void Pop_OutOfOrder_Works()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            stack.Push(ViewpointId.Overhead);
            stack.Push(ViewpointId.Cutscene);

            Assert.IsFalse(stack.Pop(ViewpointId.Overhead),
                "隠れている側を外しても今の見え方は変わらない");
            Assert.AreEqual(ViewpointId.Cutscene, stack.Active);
            Assert.AreEqual(1, stack.OverlayCount);

            Assert.IsTrue(stack.Pop(ViewpointId.Cutscene));
            Assert.AreEqual(ViewpointId.ThirdPerson, stack.Active, "基本へ戻る");
        }

        /// <summary>積まれていない視点の取り下げは無害（二重終了に強い）。</summary>
        [Test]
        public void Pop_NotPushed_IsHarmless()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            stack.Push(ViewpointId.Cutscene);

            Assert.IsFalse(stack.Pop(ViewpointId.Overhead), "積んでいないので何も起きない");
            Assert.AreEqual(ViewpointId.Cutscene, stack.Active);
            Assert.AreEqual(1, stack.OverlayCount);
        }

        /// <summary>None を渡す Pop は最前面を1枚外す。</summary>
        [Test]
        public void Pop_None_RemovesTopmost()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            stack.Push(ViewpointId.Overhead);
            stack.Push(ViewpointId.Cutscene);

            Assert.IsTrue(stack.Pop(ViewpointId.None));
            Assert.AreEqual(ViewpointId.Overhead, stack.Active, "1枚下が出る");
        }

        /// <summary>ClearOverlays は重ねを全部外す（フェーズ退場時の強制解除）。</summary>
        [Test]
        public void ClearOverlays_ReturnsToBase()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.FirstPerson);
            stack.Push(ViewpointId.Overhead);
            stack.Push(ViewpointId.Cutscene);

            Assert.IsTrue(stack.ClearOverlays());
            Assert.AreEqual(ViewpointId.FirstPerson, stack.Active);
            Assert.AreEqual(0, stack.OverlayCount);
            Assert.IsFalse(stack.ClearOverlays(), "空なら変化なし");
        }

        /// <summary>Contains で重ね中かを判定できる（照準中かどうかの分岐等）。</summary>
        [Test]
        public void Contains_ReportsOverlayPresence()
        {
            var stack = new ViewpointStack();
            stack.SetBase(ViewpointId.ThirdPerson);
            Assert.IsFalse(stack.Contains(ViewpointId.Cutscene));

            stack.Push(ViewpointId.Cutscene);
            Assert.IsTrue(stack.Contains(ViewpointId.Cutscene));
            Assert.IsFalse(stack.Contains(ViewpointId.ThirdPerson), "基本は重ねではない");
        }
    }
}
