// ============================================================================
// Seed.Input の EditMode テスト。
// エッジ検出（押した瞬間・離した瞬間）、連続値の受け渡し、ビットマスクの押下判定、
// ActionId の等価性という「基盤の約束」を、偽の読み取り実装だけで検証する。
// ============================================================================

using NUnit.Framework;
using UnityEngine;

namespace Seed.Input.Tests
{
    /// <summary>InputRouter / InputSnapshot / ActionId の基本規約テスト。</summary>
    public sealed class InputRouterTests
    {
        /// <summary>押した瞬間は1フレームだけ true になり、押しっぱなしでは false になる。</summary>
        [Test]
        public void WasPressedThisFrame_IsTrueOnlyOnTheFirstFrameOfAPress()
        {
            var reader = new FakeInputReader();
            var router = new InputRouter(reader);

            router.Tick();
            Assert.IsFalse(router.WasPressedThisFrame(ActionId.Attack), "未入力のフレームでは立たない");

            reader.SetPressed(ActionId.Attack, true);
            router.Tick();
            Assert.IsTrue(router.WasPressedThisFrame(ActionId.Attack), "押し始めたフレームで立つ");
            Assert.IsTrue(router.IsPressed(ActionId.Attack));

            router.Tick();
            Assert.IsFalse(router.WasPressedThisFrame(ActionId.Attack), "押しっぱなしでは立たない（連射防止）");
            Assert.IsTrue(router.IsPressed(ActionId.Attack), "継続入力としては押されたまま");
        }

        /// <summary>離した瞬間だけ WasReleasedThisFrame が立ち、離しっぱなしでは立たない。</summary>
        [Test]
        public void WasReleasedThisFrame_IsTrueOnlyOnTheFrameOfRelease()
        {
            var reader = new FakeInputReader();
            var router = new InputRouter(reader);

            reader.SetPressed(ActionId.Guard, true);
            router.Tick();
            router.Tick();
            Assert.IsFalse(router.WasReleasedThisFrame(ActionId.Guard), "押している間は立たない");

            reader.SetPressed(ActionId.Guard, false);
            router.Tick();
            Assert.IsTrue(router.WasReleasedThisFrame(ActionId.Guard), "離したフレームで立つ");
            Assert.IsFalse(router.IsPressed(ActionId.Guard));

            router.Tick();
            Assert.IsFalse(router.WasReleasedThisFrame(ActionId.Guard), "離しっぱなしでは立たない");
        }

        /// <summary>押して離してもう一度押すと、押した瞬間が改めて立つ（差分の基準が毎フレーム更新される）。</summary>
        [Test]
        public void WasPressedThisFrame_RisesAgain_AfterReleaseAndRepress()
        {
            var reader = new FakeInputReader();
            var router = new InputRouter(reader);

            reader.SetPressed(ActionId.Jump, true);
            router.Tick();
            reader.SetPressed(ActionId.Jump, false);
            router.Tick();
            reader.SetPressed(ActionId.Jump, true);
            router.Tick();

            Assert.IsTrue(router.WasPressedThisFrame(ActionId.Jump));
        }

        /// <summary>Move/Look は読み取り実装からそのまま渡り、Tick は1フレーム1回だけ読む。</summary>
        [Test]
        public void Tick_ForwardsMoveAndLook_AndReadsOncePerFrame()
        {
            var reader = new FakeInputReader();
            var router = new InputRouter(reader);

            reader.SetMove(new Vector2(0.5f, -1f));
            reader.SetLook(new Vector2(12f, 3f));
            router.Tick();

            Assert.AreEqual(new Vector2(0.5f, -1f), router.Move);
            Assert.AreEqual(new Vector2(12f, 3f), router.Look);
            Assert.AreEqual(new Vector2(0.5f, -1f), router.Current.Move, "Current 経由でも同じ値が読める");
            Assert.AreEqual(1, reader.ReadCount, "Tick 1回につき読み取りも1回");
        }

        /// <summary>Reset は「前＝今」に揃え、押しっぱなしのボタンを押した瞬間として湧かせない。</summary>
        [Test]
        public void Reset_AlignsPreviousWithCurrent_AndSuppressesPhantomPress()
        {
            var reader = new FakeInputReader();
            var router = new InputRouter(reader);

            reader.SetPressed(ActionId.Submit, true);
            router.Reset();

            Assert.IsTrue(router.IsPressed(ActionId.Submit), "今の状態は読み込まれている");
            Assert.IsFalse(router.WasPressedThisFrame(ActionId.Submit), "押した瞬間としては湧かない");

            router.Tick();
            Assert.IsFalse(router.WasPressedThisFrame(ActionId.Submit), "次フレームでも湧かない");
        }

        /// <summary>ビットマスクは ActionId ごとに独立し、範囲外IDは常に押されていない扱いになる。</summary>
        [Test]
        public void InputSnapshot_IsPressed_ReadsTheCorrectBit()
        {
            var mask = 0UL;
            mask = InputSnapshot.SetPressed(mask, ActionId.Attack, true);
            mask = InputSnapshot.SetPressed(mask, new ActionId(InputSnapshot.MaxButtonActionValue), true);
            var snapshot = new InputSnapshot(Vector2.zero, Vector2.zero, mask);

            Assert.IsTrue(snapshot.IsPressed(ActionId.Attack));
            Assert.IsTrue(snapshot.IsPressed(new ActionId(InputSnapshot.MaxButtonActionValue)), "上限ビットも使える");
            Assert.IsFalse(snapshot.IsPressed(ActionId.Guard), "隣のIDに漏れない");
            Assert.IsFalse(snapshot.IsPressed(ActionId.None), "None は常に押されていない");
            Assert.IsFalse(
                snapshot.IsPressed(new ActionId(InputSnapshot.MaxButtonActionValue + 1)),
                "範囲外IDは例外にせず押されていない扱い");

            mask = InputSnapshot.SetPressed(mask, ActionId.Attack, false);
            Assert.IsFalse(new InputSnapshot(Vector2.zero, Vector2.zero, mask).IsPressed(ActionId.Attack));
            Assert.IsTrue(
                new InputSnapshot(Vector2.zero, Vector2.zero, mask).IsPressed(
                    new ActionId(InputSnapshot.MaxButtonActionValue)),
                "1つ落としても他は残る");
        }

        /// <summary>範囲外IDへの SetPressed はマスクを変えない（黙って無視する）。</summary>
        [Test]
        public void InputSnapshot_SetPressed_IgnoresOutOfRangeActions()
        {
            var mask = InputSnapshot.SetPressed(0UL, new ActionId(200), true);

            Assert.AreEqual(0UL, mask);
            Assert.AreEqual(0UL, InputSnapshot.MaskOf(ActionId.None));
            Assert.AreEqual(0UL, InputSnapshot.MaskOf(new ActionId(-1)));
        }

        /// <summary>ActionId は値の等価性で比較され、辞書のキーにできる。</summary>
        [Test]
        public void ActionId_EqualityIsByValue()
        {
            var a = new ActionId(1);
            var b = new ActionId(1);
            var c = new ActionId(2);

            Assert.AreEqual(a, b);
            Assert.AreEqual(ActionId.Attack, a, "標準予約値は 1 = Attack");
            Assert.AreNotEqual(a, c);
            Assert.IsTrue(a.Equals((object)b));
            Assert.IsFalse(a.Equals("1"), "別型とは等価にならない");
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreEqual(0, ActionId.None.Value);
            Assert.AreEqual("Action#1", a.ToString());
        }

        /// <summary>読み取り実装の未配線は生成時に検知する（配線漏れを実行中まで持ち越さない）。</summary>
        [Test]
        public void Constructor_RejectsNullReader()
        {
            Assert.Throws<System.ArgumentNullException>(() => new InputRouter(null));
        }
    }
}
