using NUnit.Framework;
using Seed.Hub.Contracts;

namespace Seed.App.Tests
{
    /// <summary>マスターデータのバイナリ往復（MasterMemory ベイク→ロード）のテスト。</summary>
    public sealed class MasterBinaryTests
    {
        /// <summary>ベイクして読み戻すと、件数も中身も元のカタログと一致する。</summary>
        [Test]
        public void BakeAndLoad_RoundTripsCatalog()
        {
            var original = Sample_MasterCatalog.Build(new CharacterId(1), new CharacterId(2));

            var binary = Sample_MasterBinary.Bake(original);
            Assert.Greater(binary.Length, 0, "バイナリが出力される");

            var loaded = Sample_MasterBinary.Load(binary);

            var units = loaded.GetAll<Sample_UnitSpec>();
            var stages = loaded.GetAll<Sample_StageSpec>();
            Assert.AreEqual(original.GetAll<Sample_UnitSpec>().Count, units.Count, "ユニット件数");
            Assert.AreEqual(original.GetAll<Sample_StageSpec>().Count, stages.Count, "ステージ件数");

            var hunter = loaded.Get<Sample_UnitSpec>(1);
            Assert.AreEqual("Hunter", hunter.DebugName, "文字列が往復する");
            Assert.AreEqual(original.Get<Sample_UnitSpec>(1).MoveSpeed, hunter.MoveSpeed, 0.0001f,
                "float が往復する");

            var stage2 = loaded.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage2.Value);
            var originalStage2 = original.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage2.Value);
            Assert.AreEqual(originalStage2.DisplayName, stage2.DisplayName, "表示名が往復する");
            Assert.AreEqual(originalStage2.GroundColor.r, stage2.GroundColor.r, 0.0001f,
                "色（float 3成分）が往復する");
            Assert.AreEqual(originalStage2.EnemyAttackInterval, stage2.EnemyAttackInterval, 0.0001f);
        }

        /// <summary>同じカタログなら同じバイナリになる（ベイクの決定性＝差分レビュー可能）。</summary>
        [Test]
        public void Bake_IsDeterministic()
        {
            var a = Sample_MasterBinary.Bake(
                Sample_MasterCatalog.Build(new CharacterId(1), new CharacterId(2)));
            var b = Sample_MasterBinary.Bake(
                Sample_MasterCatalog.Build(new CharacterId(1), new CharacterId(2)));
            CollectionAssert.AreEqual(a, b, "バイト単位で一致");
        }
    }
}
