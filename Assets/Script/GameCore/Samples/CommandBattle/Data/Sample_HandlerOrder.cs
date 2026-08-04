namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】ハンドラー優先度の規約（★パターン: 発火順の決定性）。
    /// 同一イベントへの補正の適用順はゲーム仕様の一部。必ずこの定数を使い、
    /// 登録順に暗黙依存しないこと。
    /// </summary>
    public static class Sample_HandlerOrder
    {
        public const int Field = 100;   // 天気などフィールド由来
        public const int Ability = 200; // とくせい
        public const int Item = 300;    // どうぐ
    }
}
