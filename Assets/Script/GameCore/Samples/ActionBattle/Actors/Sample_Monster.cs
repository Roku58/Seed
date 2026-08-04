using System.Collections.Generic;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】モンスター。部位・状態異常ゲージ・怒り蓄積を持つ。</summary>
    public sealed class Sample_Monster : Sample_Unit
    {
        /// <summary>部位一覧。</summary>
        public IReadOnlyList<Sample_MonsterPart> Parts { get; }
        public int RageAccumulated; // 可変状態はアクター側に置く（ハンドラーは状態レス）
        /// <summary>怒りに入る累計被ダメージ閾値。</summary>
        public int RageThreshold { get; }

        private readonly Dictionary<Sample_StatusKind, Sample_StatusGauge> _gauges =
            new Dictionary<Sample_StatusKind, Sample_StatusGauge>();

        /// <summary>Sample_Monster を生成する。</summary>
        public Sample_Monster(string name, int maxHp, int rageThreshold, Sample_MonsterPart[] parts)
            : base(name, maxHp)
        {
            RageThreshold = rageThreshold;
            Parts = parts;
        }

        /// <summary>状態異常ゲージを取得する（無ければ初期化して作る）。</summary>
        public Sample_StatusGauge GetGauge(Sample_StatusKind kind, int initialTolerance)
        {
            if (!_gauges.TryGetValue(kind, out var gauge))
            {
                gauge = new Sample_StatusGauge { Tolerance = initialTolerance, ToleranceStep = initialTolerance };
                _gauges.Add(kind, gauge);
            }
            return gauge;
        }
    }
}
