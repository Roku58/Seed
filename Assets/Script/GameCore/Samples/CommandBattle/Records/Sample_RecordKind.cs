namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】事象レコードの種別。「このゲームで起こりうること」の一覧。</summary>
    public enum Sample_RecordKind
    {
        ActionDeclared,   // 「Xの 技Y！」
        ActionFailed,     // 発動失敗（まひ・まもる）
        Missed,           // 命中せず
        Hit,              // ダメージ（HPスナップショット付き）
        StatusInflicted,  // 状態異常付与
        StatusCured,      // 状態異常回復
        AbilityTriggered, // とくせい発動（割り込み）
        ItemConsumed,     // どうぐ消費（連鎖）
        Guarding,         // まもりの体勢
        TurnEnd,          // ターン終了
    }
}
