namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】View層の最小実装。レコード→文字列の変換をロジックの外で初めて行う。
    /// 実プロジェクトではここがアニメーション・UI・SEの再生キューになる。
    /// </summary>
    public static class Sample_CommandBattlePresenter
    {
        /// <summary>レコードを表示用文字列へ変換する。</summary>
        public static string Format(in Sample_Record r)
        {
            var t = $"T{r.Turn} ";
            switch (r.Kind)
            {
                case Sample_RecordKind.ActionDeclared:
                    return $"{t}{r.Actor.Name}の {r.Move.Name}！";
                case Sample_RecordKind.ActionFailed:
                    if (r.Reason == Sample_FailReason.Paralyzed)
                        return $"{t}  {r.Actor.Name}は からだがしびれて うごけない！";
                    if (r.Reason == Sample_FailReason.Fainted)
                        return $"{t}  {r.Actor.Name}は たたかえる状態ではない！";
                    return $"{t}  しかし {r.Target.Name}は まもっている！（{r.Move.Name}は失敗）";
                case Sample_RecordKind.Missed:
                    return $"{t}  {r.Actor.Name}の こうげきは 当たらなかった！";
                case Sample_RecordKind.Hit:
                {
                    var effect = r.TypePermille > Permille.One ? "／こうかは ばつぐんだ！"
                        : r.TypePermille < Permille.One ? "／こうかは いまひとつだ…" : "";
                    return $"{t}  {r.Target.Name}に {r.Value}ダメージ（残りHP {r.Value2}/{r.Target.MaxHp}）{effect}";
                }
                case Sample_RecordKind.StatusInflicted:
                    return $"{t}  {r.Target.Name}は {ConditionName(r.Condition)}状態になった！";
                case Sample_RecordKind.StatusCured:
                    return $"{t}  {r.Target.Name}の {ConditionName(r.Condition)}が治った！";
                case Sample_RecordKind.AbilityTriggered:
                    return $"{t}  [とくせい] {r.Actor.Name}の せいでんき！（割り込み）";
                case Sample_RecordKind.ItemConsumed:
                    return $"{t}  [どうぐ] {r.Actor.Name}は クラボのみを食べた！（連鎖）";
                case Sample_RecordKind.Guarding:
                    return $"{t}  {r.Actor.Name}は まもりの体勢に入った！";
                case Sample_RecordKind.TurnEnd:
                    return $"{t}─── ターン終了 ───";
                default:
                    return $"{t}{r.Kind}";
            }
        }

        /// <summary>コンディション種別の表示名を返す。</summary>
        private static string ConditionName(Sample_ConditionKind kind)
        {
            switch (kind)
            {
                case Sample_ConditionKind.Burn: return "やけど";
                case Sample_ConditionKind.Paralysis: return "まひ";
                default: return kind.ToString();
            }
        }
    }
}
