namespace Seed.Core
{
    /// <summary>
    /// コア側の可変状態のスナップショット（巻き戻し・先読みAI用）。
    /// ゲーム側の状態（HP・コンディション等）のコピーはゲーム側の責務。
    /// RecordLog は CaptureCoreSnapshot 時点の Count を控えて TruncateTo で戻す。
    ///
    /// [なぜ暴走ガードのカウンタまで持つか]
    /// 先読みAIが「解決の途中」で試し実行と巻き戻しを繰り返すと、
    /// 試し実行ぶんのイベント発火数が本番の解決に加算されたままになり、
    /// MaxEventsPerResolution の暴走ガードが誤発火する（本番の連鎖が長いほど再現しにくい）。
    /// セクション深度も同様に、巻き戻し地点の深度へ戻せないと深度ガードが誤発火する。
    /// </summary>
    public readonly struct CoreSnapshot
    {
        /// <summary>時刻（ミリ秒）。</summary>
        public readonly long NowMs;
        /// <summary>乱数の内部状態。</summary>
        public readonly uint RngState;
        /// <summary>今回の解決での発火回数（暴走ガードのカウンタ）。</summary>
        public readonly int EventsFiredInResolution;
        /// <summary>セクションの入れ子深さ（深度ガードのカウンタ）。</summary>
        public readonly int SectionDepth;

        /// <summary>
        /// 拡張状態（発火回数・セクション深度）を含むスナップショットか。
        /// 旧2引数コンストラクタで作られた値は false になり、
        /// RestoreCoreSnapshot はカウンタを触らない（＝旧コードの挙動を厳密に維持する）。
        /// </summary>
        public readonly bool HasExtendedState;

        /// <summary>CoreSnapshot を生成する（旧シグネチャ・後方互換用。カウンタは復元対象外）。</summary>
        public CoreSnapshot(long nowMs, uint rngState)
        {
            NowMs = nowMs;
            RngState = rngState;
            EventsFiredInResolution = 0;
            SectionDepth = 0;
            HasExtendedState = false;
        }

        /// <summary>CoreSnapshot を生成する（暴走ガードのカウンタまで含む完全版）。</summary>
        public CoreSnapshot(long nowMs, uint rngState, int eventsFiredInResolution, int sectionDepth)
        {
            NowMs = nowMs;
            RngState = rngState;
            EventsFiredInResolution = eventsFiredInResolution;
            SectionDepth = sectionDepth;
            HasExtendedState = true;
        }
    }
}
