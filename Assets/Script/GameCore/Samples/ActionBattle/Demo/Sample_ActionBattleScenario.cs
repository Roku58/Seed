using System.Collections.Generic;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】アクションバトルデモの進行台本。
    ///
    /// 「どんな入力を、どの順番で流すか」だけを担当する（世界の組み立ては Sample_ActionWorld、
    /// 入口とリプレイ検証は Sample_ActionBattleDemo）。
    /// 展開はフェーズごとのメソッドに分割してあり、上から読めば狩りの流れがそのまま追える。
    /// </summary>
    public static class Sample_ActionBattleScenario
    {
        /// <summary>
        /// シナリオ全体を再生する。
        /// 各入力は「記録してから実行」する（★パターン: InputJournal＝リプレイの土台）。
        /// </summary>
        public static void Play(Sample_ActionWorld world, InputJournal<Sample_ActionInput> journal)
        {
            var inputs = BuildInputs(world);

            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                journal.Record(world.Ctx.NowMs, in input);
                world.Driver.Execute(in input);
            }
        }

        /// <summary>デモの入力列をフェーズ順に組み立てる。</summary>
        private static List<Sample_ActionInput> BuildInputs(Sample_ActionWorld world)
        {
            var inputs = new List<Sample_ActionInput>(16);

            AddDrugAndTripleSlash(inputs, world);
            AddGuardedTailSwipe(inputs, world);
            AddRageTriggerCharge(inputs, world);
            AddEnragedSlash(inputs, world);
            AddStaminaExhaustedSlash(inputs, world);

            return inputs;
        }

        /// <summary>
        /// t=0.0-3.0s: 鬼人薬（攻撃+15 / 20秒）→ 頭へ斬り上げ3連。
        /// 3発目で爆破蓄積が閾値30に到達し、爆破（連鎖）→ 頭部破壊（連鎖の連鎖）が起きる。
        /// </summary>
        private static void AddDrugAndTripleSlash(List<Sample_ActionInput> inputs, Sample_ActionWorld world)
        {
            var reg = world.Registry;

            inputs.Add(Sample_ActionInput.UseDemonDrug(15, 20000));

            for (var i = 0; i < 3; i++)
            {
                inputs.Add(Sample_ActionInput.AdvanceTime(1000));
                inputs.Add(Sample_ActionInput.HunterAttack(reg.GetId(world.SlashUp), reg.GetId(world.Head)));
            }
        }

        /// <summary>t=4.0s: 尻尾回転をガード（ガード性能Lv2でチップダメージ0になる）。</summary>
        private static void AddGuardedTailSwipe(List<Sample_ActionInput> inputs, Sample_ActionWorld world)
        {
            inputs.Add(Sample_ActionInput.AdvanceTime(1000));
            inputs.Add(Sample_ActionInput.MonsterAttack(world.Registry.GetId(world.TailSwipe), hunterGuarded: true));
        }

        /// <summary>
        /// t=22.0s: 時間経過で鬼人薬が失効（失効レコードが出る）。
        /// 溜め斬りで累計被ダメージが閾値を超え、モンスターが怒り状態に入る。
        /// </summary>
        private static void AddRageTriggerCharge(List<Sample_ActionInput> inputs, Sample_ActionWorld world)
        {
            var reg = world.Registry;

            inputs.Add(Sample_ActionInput.AdvanceTime(18000));
            inputs.Add(Sample_ActionInput.HunterAttack(reg.GetId(world.ChargedSlash), reg.GetId(world.Head)));
        }

        /// <summary>t=23.0s: 怒り（被ダメx0.9）で、同じ斬り上げのダメージが下がるのを見せる。</summary>
        private static void AddEnragedSlash(List<Sample_ActionInput> inputs, Sample_ActionWorld world)
        {
            var reg = world.Registry;

            inputs.Add(Sample_ActionInput.AdvanceTime(1000));
            inputs.Add(Sample_ActionInput.HunterAttack(reg.GetId(world.SlashUp), reg.GetId(world.Head)));
        }

        /// <summary>t=24.0s: スタミナ不足で発動判定に弾かれる（行動不可レコードが出る）。</summary>
        private static void AddStaminaExhaustedSlash(List<Sample_ActionInput> inputs, Sample_ActionWorld world)
        {
            var reg = world.Registry;

            inputs.Add(Sample_ActionInput.AdvanceTime(1000));
            inputs.Add(Sample_ActionInput.HunterAttack(reg.GetId(world.SlashUp), reg.GetId(world.Head)));
        }
    }
}
