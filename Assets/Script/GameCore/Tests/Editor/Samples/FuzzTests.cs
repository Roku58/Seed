// ============================================================================
// Fuzzテスト: ランダムな入力列を大量に流し、
// 「例外・暴走ガードが発火しない」「リプレイが常に一致する」ことを確認する。
// 生成にも DeterministicRandom を使うため、失敗したシードで完全再現できる。
// ============================================================================

using NUnit.Framework;
using Seed.Core;
using AB = Seed.Core.Samples.ActionBattle;

namespace Seed.Core.Tests
{
    /// <summary>ランダム入力によるFuzzテスト。</summary>
    public sealed class FuzzTests
    {
        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ActionBattle_RandomInputs_NoExplosion_AndReplayAlwaysMatches()
        {
            for (uint seed = 100; seed < 105; seed++)
            {
                var world = new AB.Sample_ActionWorld(seed, trace: null);
                var journal = new InputJournal<AB.Sample_ActionInput>(256) { Seed = seed, Version = 1 };
                world.Ctx.AddExtension(journal);

                var gen = new DeterministicRandom(seed ^ 0xF00DF00Du); // 入力生成用（ロジック乱数とは別）
                for (var i = 0; i < 120; i++)
                {
                    var input = NextRandomInput(world, gen);
                    journal.Record(world.Ctx.NowMs, in input);
                    world.Driver.Execute(in input); // 例外・暴走ガード発火なしで完走すること
                }

                // リプレイは常に一致する
                var replay = new AB.Sample_ActionWorld(seed, trace: null);
                for (var i = 0; i < journal.Count; i++)
                {
                    replay.Driver.Execute(journal[i].Input);
                }
                Assert.AreEqual(
                    AB.Sample_ActionBattleDemo.HashRecords(world.Ctx),
                    AB.Sample_ActionBattleDemo.HashRecords(replay.Ctx),
                    $"seed={seed} でリプレイ不一致（決定性が壊れている）");
            }
        }

        /// <summary>NextRandomInput を生成する。</summary>
        private static AB.Sample_ActionInput NextRandomInput(AB.Sample_ActionWorld world, DeterministicRandom gen)
        {
            var registry = world.Registry;
            switch (gen.NextInt(10))
            {
                case 0:
                case 1:
                case 2:
                    return AB.Sample_ActionInput.AdvanceTime(100 + gen.NextInt(1900));
                case 3:
                    return AB.Sample_ActionInput.UseDemonDrug(5 + gen.NextInt(20), 3000 + gen.NextInt(20000));
                case 4:
                case 5:
                case 6:
                case 7:
                {
                    var move = gen.NextInt(2) == 0 ? world.SlashUp : world.ChargedSlash;
                    var part = gen.NextInt(2) == 0 ? world.Head : world.Wing;
                    return AB.Sample_ActionInput.HunterAttack(registry.GetId(move), registry.GetId(part));
                }
                default:
                    return AB.Sample_ActionInput.MonsterAttack(
                        registry.GetId(world.TailSwipe), gen.NextInt(2) == 0);
            }
        }
    }
}
