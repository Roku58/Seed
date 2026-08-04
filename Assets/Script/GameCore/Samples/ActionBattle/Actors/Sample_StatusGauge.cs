namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】状態異常の蓄積ゲージ（可変状態はアクター側に置く＝ハンドラー状態レス化）。</summary>
    public sealed class Sample_StatusGauge
    {
        /// <summary>現在の蓄積値。</summary>
        public int Accumulated;
        public int Tolerance;      // 現在の閾値
        public int ToleranceStep;  // 発動ごとの耐性上昇量
    }
}
