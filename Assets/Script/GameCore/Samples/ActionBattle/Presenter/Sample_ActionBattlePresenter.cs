namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】View層の最小実装（★パターン: ID方式レコードの解決）。
    /// レコードはIDしか持たないため、EntityRegistry で実体を引き直して表示する。
    /// </summary>
    public static class Sample_ActionBattlePresenter
    {
        /// <summary>レコードを表示用文字列へ変換する。</summary>
        public static string Format(in Sample_Record r, EntityRegistry registry)
        {
            var actor = registry.GetEntity<Sample_Unit>(r.ActorId);
            var target = registry.GetEntity<Sample_Unit>(r.TargetId);
            var part = registry.GetEntity<Sample_MonsterPart>(r.PartId);
            var move = registry.GetEntity<Sample_AttackMove>(r.MoveId);
            var t = $"[{r.TimeMs / 1000}.{r.TimeMs % 1000 / 100}s] ";

            switch (r.Kind)
            {
                case Sample_RecordKind.ItemUsed:
                    return $"{t}{actor.Name}: {ConditionName(r.Condition)}を使用（攻撃+{r.Value} / {r.Value2 / 1000}秒）";
                case Sample_RecordKind.ActionBlocked:
                    if (r.Block == Sample_BlockReason.Stamina)
                        return $"{t}{actor.Name}: {move.Name} は出せない（スタミナ不足 {r.Value}/{r.Value2}）";
                    if (r.Block == Sample_BlockReason.Dead)
                        return $"{t}{actor.Name}: {move.Name} は出せない（戦闘不能）";
                    return $"{t}{actor.Name}: {move.Name} は出せない（拘束中）";
                case Sample_RecordKind.HitDamage:
                {
                    var partText = part != null ? $"の{part.Name}" : "";
                    var crit = r.Flag ? "（会心！）" : "";
                    return $"{t}{actor.Name}の {move.Name} → {target.Name}{partText} に {r.Value}ダメージ{crit}  [残りHP {r.Value2}/{target.MaxHp}]";
                }
                case Sample_RecordKind.GuardChip:
                    return $"{t}{target.Name}は {actor.Name}の {move.Name} をガード（チップダメージ {r.Value}）";
                case Sample_RecordKind.StatusBuildup:
                    return $"{t}  {StatusName(r.Status)}蓄積 {r.Value}/{r.Value2}";
                case Sample_RecordKind.StatusTriggered:
                    if (r.Status == Sample_StatusKind.Blast)
                        return $"{t}{StatusName(r.Status)}が発動！ {part?.Name}に {r.Value}ダメージ  [残りHP {r.Value2}/{target.MaxHp}]";
                    return $"{t}{StatusName(r.Status)}が発動！ {target.Name}は {r.Value2 / 1000}秒間 拘束された";
                case Sample_RecordKind.PartBroken:
                    return $"{t}部位破壊！ {part.Name}（肉質→{r.Value}）";
                case Sample_RecordKind.ConditionAdded:
                    if (r.Condition == Sample_ConditionKind.Enraged)
                        return $"{t}{actor.Name}は 怒り状態になった（被ダメx0.9 / {r.Value2 / 1000}秒）";
                    return $"{t}{actor.Name}: {ConditionName(r.Condition)} 付与";
                case Sample_RecordKind.ConditionExpired:
                    return $"{t}{actor.Name}の {ConditionName(r.Condition)} が切れた";
                default:
                    return $"{t}{r.Kind}";
            }
        }

        /// <summary>コンディション種別の表示名を返す。</summary>
        private static string ConditionName(Sample_ConditionKind kind)
        {
            switch (kind)
            {
                case Sample_ConditionKind.DemonDrug: return "鬼人薬";
                case Sample_ConditionKind.Enraged: return "怒り";
                case Sample_ConditionKind.Paralyzed: return "まひ";
                default: return kind.ToString();
            }
        }

        /// <summary>状態異常種別の表示名を返す。</summary>
        private static string StatusName(Sample_StatusKind kind)
        {
            switch (kind)
            {
                case Sample_StatusKind.Blast: return "爆破";
                case Sample_StatusKind.Paralysis: return "まひ";
                default: return kind.ToString();
            }
        }
    }
}
