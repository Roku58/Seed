namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】ハンドラー優先度の規約（★パターン: 発火順の決定性）。
    /// 攻撃力補正が常に 斬れ味→スキル→アイテム の順で掛かることを保証する。
    /// 整数演算では掛け算の順番で結果が変わるため、この順序はゲーム仕様の一部。
    /// </summary>
    public static class Sample_HandlerOrder
    {
        public const int Weapon = 100;  // 武器固有（斬れ味・属性）
        public const int Skill = 200;   // 装備スキル
        public const int Item = 300;    // アイテムバフ
        public const int Monster = 400; // モンスター側の状態（怒り等）
    }
}
