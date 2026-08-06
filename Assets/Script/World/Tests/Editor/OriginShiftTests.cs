using NUnit.Framework;
using UnityEngine;

namespace Seed.World.Tests
{
    /// <summary>原点回帰の判断（閾値・丸め・累積・座標変換）のテスト。純C#。</summary>
    public sealed class OriginShiftTests
    {
        /// <summary>閾値の内側では何も起きない。</summary>
        [Test]
        public void Shifter_InsideThreshold_DoesNothing()
        {
            var shifter = new OriginShifter(threshold: 1000f);
            Assert.IsFalse(shifter.ShouldShift(new Vector3(500f, 0f, 500f)), "距離707mは閾値内");
            Assert.IsFalse(shifter.TryShift(new Vector3(500f, 0f, 500f), out var delta));
            Assert.AreEqual(Vector3.zero, delta);
            Assert.AreEqual(0, shifter.ShiftCount);
        }

        /// <summary>閾値を超えたら焦点を原点へ戻す量を返す。</summary>
        [Test]
        public void Shifter_BeyondThreshold_ReturnsFocusToOrigin()
        {
            var shifter = new OriginShifter(threshold: 1000f);
            var focus = new Vector3(1200f, 5f, -900f);

            Assert.IsTrue(shifter.TryShift(focus, out var delta));
            Assert.AreEqual(new Vector3(-1200f, 0f, 900f), delta, "水平のみ・焦点を原点へ");
            Assert.AreEqual(1, shifter.ShiftCount);
            Assert.AreEqual(new Vector3(-1200f, 0f, 900f), shifter.TotalOffset, "累積へ記録");
        }

        /// <summary>高さは既定でずらさない（縦に伸びないゲームでは触らないのが安全）。</summary>
        [Test]
        public void Shifter_VerticalIgnoredByDefault()
        {
            var shifter = new OriginShifter(threshold: 100f);
            Assert.IsFalse(shifter.ShouldShift(new Vector3(0f, 5000f, 0f)),
                "高さだけ大きくてもシフトしない");

            shifter.ShiftVertical = true;
            Assert.IsTrue(shifter.ShouldShift(new Vector3(0f, 5000f, 0f)), "有効にすれば見る");
            shifter.TryShift(new Vector3(0f, 5000f, 0f), out var delta);
            Assert.AreEqual(-5000f, delta.y, 0.001f, "高さもずらす");
        }

        /// <summary>格子への丸め（模様の位相を保つため）。</summary>
        [Test]
        public void Shifter_SnapsDeltaToGrid()
        {
            var shifter = new OriginShifter(threshold: 100f, snapSize: 500f);
            var delta = shifter.ComputeDelta(new Vector3(1234f, 0f, -678f));

            Assert.AreEqual(-1000f, delta.x, 0.001f, "1234 → 1000 の格子へ");
            Assert.AreEqual(500f, delta.z, 0.001f, "-678 → -500 の格子へ");
        }

        /// <summary>丸めの結果ずらし量が0になる場合はシフトしない（無駄打ちを避ける）。</summary>
        [Test]
        public void Shifter_SnapToZero_SkipsShift()
        {
            var shifter = new OriginShifter(threshold: 100f, snapSize: 1000f);
            // 距離は閾値超えだが、1000m 格子に丸めると 0 になる位置
            Assert.IsFalse(shifter.TryShift(new Vector3(200f, 0f, 200f), out var delta));
            Assert.AreEqual(Vector3.zero, delta);
            Assert.AreEqual(0, shifter.ShiftCount);
        }

        /// <summary>累積量は加算され、絶対座標との往復ができる。</summary>
        [Test]
        public void Shifter_AccumulatesOffset_AndConvertsBack()
        {
            var shifter = new OriginShifter(threshold: 1000f);
            shifter.TryShift(new Vector3(1500f, 0f, 0f), out _);   // -1500
            shifter.TryShift(new Vector3(0f, 0f, -2000f), out _);  // +2000（z）

            Assert.AreEqual(new Vector3(-1500f, 0f, 2000f), shifter.TotalOffset);
            Assert.AreEqual(2, shifter.ShiftCount);

            // 今の見た目で原点にいるキャラは、開始時の座標系では (1500, 0, -2000)
            Assert.AreEqual(new Vector3(1500f, 0f, -2000f), shifter.ToAbsolute(Vector3.zero),
                "見た目→絶対");
            Assert.AreEqual(Vector3.zero, shifter.ToShifted(new Vector3(1500f, 0f, -2000f)),
                "絶対→見た目");
        }

        /// <summary>ComputeDelta は状態を変えない（判断と確定を分けている）。</summary>
        [Test]
        public void Shifter_ComputeDelta_IsPure()
        {
            var shifter = new OriginShifter(threshold: 100f);
            shifter.ComputeDelta(new Vector3(500f, 0f, 0f));
            shifter.ComputeDelta(new Vector3(500f, 0f, 0f));

            Assert.AreEqual(0, shifter.ShiftCount, "計算だけでは確定しない");
            Assert.AreEqual(Vector3.zero, shifter.TotalOffset);
        }

        /// <summary>手動 Commit でも累積と変換は保たれる（テレポート後の整え直し）。</summary>
        [Test]
        public void Shifter_ManualCommit_KeepsConversion()
        {
            var shifter = new OriginShifter();
            shifter.Commit(new Vector3(-100f, 0f, 50f));

            Assert.AreEqual(1, shifter.ShiftCount);
            Assert.AreEqual(new Vector3(100f, 0f, -50f), shifter.ToAbsolute(Vector3.zero));
        }
    }
}
