namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】事象レコードの種別。</summary>
    public enum Sample_RecordKind
    {
        /// <summary>アイテム使用。</summary>
        ItemUsed,
        /// <summary>発動判定による行動不可。</summary>
        ActionBlocked,
        /// <summary>ヒットダメージ。</summary>
        HitDamage,
        /// <summary>ガード成立（チップダメージ）。</summary>
        GuardChip,
        /// <summary>状態異常の蓄積。</summary>
        StatusBuildup,
        /// <summary>状態異常の発動。</summary>
        StatusTriggered,
        /// <summary>部位破壊。</summary>
        PartBroken,
        /// <summary>コンディション付与（怒り・バフ等）。</summary>
        ConditionAdded,
        /// <summary>コンディション失効。</summary>
        ConditionExpired,
        /// <summary>
        /// 戦闘不能（HPが0になった瞬間に1回だけ出る）。
        /// 消費側（Bridge等）が「残りHP&lt;=0」からの死亡推論をしなくて済むよう、
        /// 真実の持ち主であるロジック側が明示的に発行する。
        /// 末尾追加なのは既存レコードのハッシュ値（リプレイ検証）を変えないため。
        /// </summary>
        Defeated,
    }
}
