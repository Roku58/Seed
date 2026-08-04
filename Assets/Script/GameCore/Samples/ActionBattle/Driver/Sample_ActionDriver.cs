using System.Collections.Generic;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】アクション層の進行役。
    /// 実プロジェクトではモーション・コリジョン・AI・ガード姿勢判定がこの層の責務で、
    /// 結果だけを HitRequest に詰めて事象計算層へ渡す。
    /// 入力はすべて Sample_ActionInput（ID参照のstruct）で受ける＝そのままリプレイ可能。
    /// </summary>
    public sealed class Sample_ActionDriver
    {
        /// <summary>スタミナ自然回復量（毎秒）。</summary>
        private const int StaminaRegenPerSecond = 5;

        /// <summary>実行コンテキスト。</summary>
        private readonly LogicContext _ctx;
        /// <summary>操作対象のハンター。</summary>
        private readonly Sample_Hunter _hunter;
        /// <summary>対象のモンスター。</summary>
        private readonly Sample_Monster _monster;
        private readonly List<TimedCondition<Sample_ConditionKind>> _expiredBuffer =
            new List<TimedCondition<Sample_ConditionKind>>(4);

        /// <summary>Sample_ActionDriver を生成する。</summary>
        public Sample_ActionDriver(LogicContext ctx, Sample_Hunter hunter, Sample_Monster monster)
        {
            _ctx = ctx;
            _hunter = hunter;
            _monster = monster;
        }

        /// <summary>入力を1件実行する（★パターン: リプレイは同じ入力列を先頭から流し直すだけ）。</summary>
        public void Execute(in Sample_ActionInput input)
        {
            var registry = Sample_ActionContext.Registry(_ctx);
            switch (input.Kind)
            {
                case Sample_ActionInputKind.AdvanceTime:
                    AdvanceTimeMs(input.Value);
                    break;
                case Sample_ActionInputKind.UseDemonDrug:
                    UseDemonDrug(input.Value, input.Value2);
                    break;
                case Sample_ActionInputKind.HunterAttack:
                    HunterAttack(
                        registry.GetEntity<Sample_AttackMove>(input.MoveId),
                        registry.GetEntity<Sample_MonsterPart>(input.PartId));
                    break;
                case Sample_ActionInputKind.MonsterAttack:
                    MonsterAttack(
                        registry.GetEntity<Sample_AttackMove>(input.MoveId),
                        input.Flag);
                    break;
            }
        }

        /// <summary>時間を進め、スタミナ回復と失効処理を行う。</summary>
        public void AdvanceTimeMs(long ms)
        {
            _ctx.AdvanceTime(ms);
            ((Sample_IUnitWriter)_hunter).RegenStamina((int)(ms * StaminaRegenPerSecond / 1000));
            DrainExpired(_hunter);
            DrainExpired(_monster);
        }

        /// <summary>失効したコンディションを取り除き、失効レコードを出す。</summary>
        private void DrainExpired(Sample_Unit unit)
        {
            _expiredBuffer.Clear();
            unit.Conditions.RemoveExpired(_ctx.NowMs, _expiredBuffer);
            for (var i = 0; i < _expiredBuffer.Count; i++)
            {
                Sample_ActionContext.AddRecord(_ctx, new Sample_Record(_ctx.NowMs,
                    Sample_RecordKind.ConditionExpired,
                    actorId: Sample_ActionContext.Id(_ctx, unit),
                    condition: _expiredBuffer[i].Kind));
            }
        }

        /// <summary>鬼人薬を使用する（Extendポリシーで延長）。</summary>
        public void UseDemonDrug(int attackBonus, long durationMs)
        {
            _ctx.BeginResolution();
            // ★パターン: ConditionMergePolicy.Extend（飲み直しは効果時間の延長）
            ((Sample_IUnitWriter)_hunter).AddConditionMerged(new TimedCondition<Sample_ConditionKind>(
                Sample_ConditionKind.DemonDrug, _ctx.NowMs + durationMs, attackBonus),
                ConditionMergePolicy.Extend, _ctx.NowMs);
            Sample_ActionContext.AddRecord(_ctx, new Sample_Record(_ctx.NowMs, Sample_RecordKind.ItemUsed,
                actorId: Sample_ActionContext.Id(_ctx, _hunter), condition: Sample_ConditionKind.DemonDrug,
                value: attackBonus, value2: (int)durationMs));
        }

        /// <summary>ハンターの攻撃（発動判定→ヒット解決）を実行する。</summary>
        public void HunterAttack(Sample_AttackMove move, Sample_MonsterPart targetPart)
        {
            _ctx.BeginResolution();
            if (!_ctx.RunSection(Sample_ActivationCheckSection.Instance,
                    new Sample_ActivationInput(_hunter, move))) return;

            // 実際はここで: モーション再生 → 有効フレームでコリジョン → 当たれば HitRequest 生成
            _ctx.RunSection(Sample_HitResolutionSection.Instance,
                new Sample_HitRequest(_hunter, _monster, targetPart, move, wasGuarded: false));
        }

        /// <summary>モンスターの攻撃（同じセクションを再利用）を実行する。</summary>
        public void MonsterAttack(Sample_AttackMove move, bool hunterGuarded)
        {
            _ctx.BeginResolution();
            if (!_ctx.RunSection(Sample_ActivationCheckSection.Instance,
                    new Sample_ActivationInput(_monster, move))) return;

            _ctx.RunSection(Sample_HitResolutionSection.Instance,
                new Sample_HitRequest(_monster, _hunter, null, move, hunterGuarded));
        }
    }
}
