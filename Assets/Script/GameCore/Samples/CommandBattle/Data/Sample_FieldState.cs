namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>【サンプル】フィールド状態（LogicContext の拡張として登録するゲーム固有状態の例）。</summary>
    public sealed class Sample_FieldState
    {
        /// <summary>現在の天気。</summary>
        public Sample_Weather Weather;
        /// <summary>ターン番号。</summary>
        public int Turn = 1;
    }
}
