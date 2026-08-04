namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】リプレイ可能な入力の種別（★パターン: InputJournal）。</summary>
    public enum Sample_ActionInputKind
    {
        /// <summary>時間経過。</summary>
        AdvanceTime,
        /// <summary>鬼人薬の使用。</summary>
        UseDemonDrug,
        /// <summary>ハンターの攻撃。</summary>
        HunterAttack,
        /// <summary>モンスターの攻撃。</summary>
        MonsterAttack,
    }
}
