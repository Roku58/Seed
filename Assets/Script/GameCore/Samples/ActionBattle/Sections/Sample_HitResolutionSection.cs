using System;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】ヒット解決セクション：1ヒット分の事象計算パイプライン全体。
    /// 「命中判定」は存在しない（アクション層のコリジョンが担う＝セクション取捨選択の例）。
    /// </summary>
    public sealed class Sample_HitResolutionSection : Section<Sample_HitRequest, Sample_HitResult>
    {
        /// <summary>会心倍率(‰)。</summary>
        public const int CritMultiplierPermille = 1250;

        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_HitResolutionSection Instance = new Sample_HitResolutionSection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "ヒット解決";
        /// <summary>Sample_HitResolutionSection を生成する。</summary>
        private Sample_HitResolutionSection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override Sample_HitResult Execute(LogicContext ctx, in Sample_HitRequest input)
        {
            // 1. 攻撃力決定（斬れ味→スキル→鬼人薬 の順。順序は Sample_HandlerOrder が保証）
            int attack;
            using (var scope = EventScope<Sample_AttackPowerEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.Attacker;
                ev.Move = input.Move;
                ev.Value = input.Attacker is Sample_Hunter h ? h.Weapon.Attack : input.Move.BaseAttack;
                ctx.Hub.Fire(ev, ctx);
                attack = ev.Value;
            }

            // 2. 肉質決定（傷・軟化の介入ポイント）
            int hitZone;
            using (var scope = EventScope<Sample_HitZoneEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Defender = input.Defender;
                ev.Part = input.Part;
                ev.Percent = input.Part != null ? input.Part.HitZonePercent : 100;
                ctx.Hub.Fire(ev, ctx);
                hitZone = ev.Percent;
            }

            // 3. 会心判定（弱点特効の介入ポイント。率0以下は乱数を消費しない）
            int critRate;
            using (var scope = EventScope<Sample_CritRateEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.Attacker;
                ev.Defender = input.Defender;
                ev.HitZonePercent = hitZone;
                ev.RatePermille = input.Attacker is Sample_Hunter h2 ? h2.Weapon.AffinityPermille : 0;
                ctx.Hub.Fire(ev, ctx);
                critRate = ev.RatePermille;
            }
            var isCrit = ctx.LogicRandom.Roll(critRate);

            // 4. ダメージ算出（整数演算）＋ 最終補正イベント（ガード・怒り）
            var value = attack * input.Move.MotionValuePercent / 100;
            value = value * hitZone / 100;
            if (isCrit)
            {
                value = Permille.Apply(value, CritMultiplierPermille);
            }

            int damage;
            using (var scope = EventScope<Sample_FinalDamageEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.Attacker;
                ev.Defender = input.Defender;
                ev.WasGuarded = input.WasGuarded;
                ev.Value = value;
                ctx.Hub.Fire(ev, ctx);
                damage = Math.Max(0, ev.Value);
            }

            // 5. HP反映（レコードにはHPスナップショットとIDを焼き込む）
            var wasDead = input.Defender.IsDead;
            ((Sample_IUnitWriter)input.Defender).ApplyDamage(damage);
            Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs,
                input.WasGuarded ? Sample_RecordKind.GuardChip : Sample_RecordKind.HitDamage,
                actorId: Sample_ActionContext.Id(ctx, input.Attacker),
                targetId: Sample_ActionContext.Id(ctx, input.Defender),
                partId: Sample_ActionContext.Id(ctx, input.Part),
                moveId: Sample_ActionContext.Id(ctx, input.Move),
                value: damage, value2: input.Defender.Hp, flag: isCrit));

            // 戦闘不能は真実の持ち主が明示発行する（消費側にHP推論をさせない）
            if (!wasDead && input.Defender.IsDead)
            {
                Sample_ActionContext.AddRecord(ctx, new Sample_Record(ctx.NowMs,
                    Sample_RecordKind.Defeated,
                    actorId: Sample_ActionContext.Id(ctx, input.Attacker),
                    targetId: Sample_ActionContext.Id(ctx, input.Defender)));
            }

            var total = damage;

            // 6. 部位耐久減少
            if (input.Part != null && damage > 0)
            {
                ctx.RunSection(Sample_PartDamageSection.Instance,
                    new Sample_PartDamageInput(input.Part, damage));
            }

            // 7. 状態異常蓄積（ガード時は蓄積しない）
            if (!input.WasGuarded && damage > 0
                && input.Attacker is Sample_Hunter hunter && hunter.Weapon.Status != Sample_StatusKind.None
                && input.Defender is Sample_Monster monster)
            {
                total += ctx.RunSection(Sample_StatusBuildupSection.Instance, new Sample_StatusInput(
                    monster, input.Part, hunter.Weapon.Status, hunter.Weapon.StatusBuildupPerHit));
            }

            // 8. リアクション（怒り蓄積などの割り込み起点）
            using (var scope = EventScope<Sample_HitResolvedEvent>.Rent())
            {
                var ev = scope.Event;
                ev.Attacker = input.Attacker;
                ev.Defender = input.Defender;
                ev.TotalDamage = total;
                ctx.Hub.Fire(ev, ctx);
            }

            return new Sample_HitResult(total);
        }
    }
}
