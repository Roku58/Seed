namespace Seed.Core.Presenter
{
    /// <summary>
    /// float秒（Unityのフレーム時間）→ long ミリ秒（ロジック時間）の境界変換。
    /// ミリ秒未満の端数を繰り越して切り捨て誤差の蓄積を防ぐ。
    ///
    /// 「float を秒のままロジックに入れない」規約の境界に立つ道具で、
    /// 各Runner・フェーズが同じ端数処理を手書きで重複させないためにここに一本化した。
    /// </summary>
    public struct LogicTimeAccumulator
    {
        /// <summary>ミリ秒未満の繰り越し。</summary>
        private float _carryMs;

        /// <summary>経過秒を加算し、ロジックへ渡すべき整数ミリ秒を返す（0のこともある）。</summary>
        public int Advance(float deltaSeconds)
        {
            _carryMs += deltaSeconds * 1000f;
            var ms = (int)_carryMs;
            if (ms > 0)
            {
                _carryMs -= ms;
            }
            return ms > 0 ? ms : 0;
        }

        /// <summary>繰り越しを破棄する（フェーズ再入時など）。</summary>
        public void Reset()
        {
            _carryMs = 0f;
        }
    }
}
