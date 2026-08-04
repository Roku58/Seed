namespace Seed.Core
{
    /// <summary>LogicContext の動作設定。</summary>
    public readonly struct LogicContextConfig
    {
        /// <summary>ロジック乱数のシード（ロジックへの「入力」として扱う）。</summary>
        public readonly uint LogicSeed;

        /// <summary>セクション再帰の深度上限（連鎖の暴走検知）。</summary>
        public readonly int MaxSectionDepth;

        /// <summary>1解決（攻撃1回等）あたりのイベント発火数上限（連鎖の暴走検知）。</summary>
        public readonly int MaxEventsPerResolution;

        /// <summary>LogicContextConfig を生成する。</summary>
        public LogicContextConfig(uint logicSeed, int maxSectionDepth = 16, int maxEventsPerResolution = 128)
        {
            LogicSeed = logicSeed;
            MaxSectionDepth = maxSectionDepth;
            MaxEventsPerResolution = maxEventsPerResolution;
        }
    }
}
