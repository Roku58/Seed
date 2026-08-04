// ============================================================================
// サンプル2種（コマンド/アクション）を通した統合テスト。
// [設計原則: シード固定のゴールデンログテスト / 物語アサーション / パラメタライズド]
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;
using CB = Seed.Core.Samples.CommandBattle;
using AB = Seed.Core.Samples.ActionBattle;

namespace Seed.Core.Tests
{
    /// <summary>サンプル2種を通した統合テスト。</summary>
    public sealed class SampleIntegrationTests
    {
        // ---------------- 決定性（ゴールデンログ） ----------------

        /// <summary>同じシードなら事象レコード列は完全に一致する。</summary>
        [Test]
        public void CommandBattle_SameSeed_ProducesIdenticalRecords()
        {
            var first = FormatCommandRecords(CB.Sample_CommandBattleDemo.RunDemo());
            var second = FormatCommandRecords(CB.Sample_CommandBattleDemo.RunDemo());
            CollectionAssert.AreEqual(first, second);
        }

        /// <summary>テスト。検証内容はメソッド名のとおり。</summary>
        [Test]
        public void ActionBattle_SameSeed_ProducesIdenticalRecords()
        {
            var first = AB.Sample_ActionBattleDemo.HashRecords(AB.Sample_ActionBattleDemo.RunDemo());
            var second = AB.Sample_ActionBattleDemo.HashRecords(AB.Sample_ActionBattleDemo.RunDemo());
            Assert.AreEqual(first, second);
        }

        /// <summary>★パターン検証: InputJournal によるリプレイが元の結果を完全再現する。</summary>
        [Test]
        public void ActionBattle_Replay_ReproducesIdenticalResult()
        {
            var ctx = AB.Sample_ActionBattleDemo.RunDemo();
            var match = AB.Sample_ActionBattleDemo.VerifyReplay(ctx, out var originalHash, out var replayHash);

            Assert.IsTrue(match, $"original={originalHash:X8} replay={replayHash:X8}");
        }

        // ---------------- 物語アサーション ----------------

        /// <summary>コマンドバトルデモの物語（割り込み・連鎖・まもる）が成立していること。</summary>
        [Test]
        public void CommandBattle_Demo_ContainsInterruptChainAndProtect()
        {
            var ctx = CB.Sample_CommandBattleDemo.RunDemo();
            var log = ctx.GetExtension<RecordLog<CB.Sample_Record>>();

            Assert.IsTrue(ContainsCommand(log, CB.Sample_RecordKind.AbilityTriggered), "せいでんきの割り込みが発生する");
            Assert.IsTrue(ContainsCommand(log, CB.Sample_RecordKind.ItemConsumed), "クラボのみの連鎖が発生する");
            Assert.IsTrue(ContainsCommand(log, CB.Sample_RecordKind.StatusCured), "まひが治る");
            Assert.IsTrue(ContainsCommand(log, CB.Sample_RecordKind.ActionFailed), "まもるで技が失敗する");
        }

        /// <summary>★パターン検証: 巻き戻し。お試しターンの痕跡が最終ログに残らない（ターン数=2）。</summary>
        [Test]
        public void CommandBattle_RollbackTrialTurn_LeavesNoTrace()
        {
            var ctx = CB.Sample_CommandBattleDemo.RunDemo();
            var log = ctx.GetExtension<RecordLog<CB.Sample_Record>>();

            var turnEnds = 0;
            for (var i = 0; i < log.Count; i++)
            {
                if (log[i].Kind == CB.Sample_RecordKind.TurnEnd)
                {
                    turnEnds++;
                }
            }
            Assert.AreEqual(2, turnEnds, "お試しターン（3つ目のTurnEnd）は巻き戻されて存在しない");
        }

        /// <summary>アクションバトルデモの物語（爆破連鎖・部位破壊・怒り・ガード・スタミナ）。</summary>
        [Test]
        public void ActionBattle_Demo_ContainsBlastChainPartBreakAndRage()
        {
            var ctx = AB.Sample_ActionBattleDemo.RunDemo();
            var log = ctx.GetExtension<RecordLog<AB.Sample_Record>>();

            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.StatusTriggered), "爆破が発動する");
            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.PartBroken), "部位破壊が発生する");
            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.ConditionAdded), "怒り状態が付与される");
            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.ActionBlocked), "スタミナ不足で発動判定に弾かれる");
            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.GuardChip), "ガードが機能する");
            Assert.IsTrue(ContainsAction(log, AB.Sample_RecordKind.ConditionExpired), "鬼人薬が失効する");
        }

        // ---------------- パラメタライズド（連鎖の回帰防止） ----------------

        /// <summary>爆破蓄積: 閾値30 → 3ヒット目(蓄積10x3)で発動する。</summary>
        [TestCase(2, false)]
        [TestCase(3, true)]
        public void ActionBattle_BlastBuildup_TriggersAtTolerance(int hits, bool expectTriggered)
        {
            var ctx = new LogicContext(1);
            ctx.AddExtension(new RecordLog<AB.Sample_Record>());
            var registry = new EntityRegistry();
            ctx.AddExtension(registry);

            var head = new AB.Sample_MonsterPart("頭", 65, 200, 10);
            var monster = new AB.Sample_Monster("テスト竜", 2000, 300, new[] { head });
            registry.Register(head);
            registry.Register(monster);

            var triggered = false;
            for (var i = 0; i < hits; i++)
            {
                ctx.BeginResolution();
                var chain = ctx.RunSection(AB.Sample_StatusBuildupSection.Instance,
                    /// <summary>Sample_StatusInput を生成する。</summary>
                    new AB.Sample_StatusInput(monster, head, AB.Sample_StatusKind.Blast, 10));
                triggered |= chain > 0;
            }

            Assert.AreEqual(expectTriggered, triggered);
        }

        // ---------------- helpers ----------------

        /// <summary>ContainsCommand を生成する。</summary>
        private static bool ContainsCommand(RecordLog<CB.Sample_Record> log, CB.Sample_RecordKind kind)
        {
            for (var i = 0; i < log.Count; i++)
            {
                if (log[i].Kind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>ContainsAction を生成する。</summary>
        private static bool ContainsAction(RecordLog<AB.Sample_Record> log, AB.Sample_RecordKind kind)
        {
            for (var i = 0; i < log.Count; i++)
            {
                if (log[i].Kind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>FormatCommandRecords を生成する。</summary>
        private static List<string> FormatCommandRecords(LogicContext ctx)
        {
            var log = ctx.GetExtension<RecordLog<CB.Sample_Record>>();
            var lines = new List<string>(log.Count);
            for (var i = 0; i < log.Count; i++)
            {
                lines.Add(CB.Sample_CommandBattlePresenter.Format(log[i]));
            }
            return lines;
        }
    }
}
