namespace Seed.Core
{
    /// <summary>
    /// 決定的乱数（xorshift32）。
    ///
    /// [設計原則: 決定性 / 乱数の分離]
    /// - System.Random はランタイム実装差のリスクがあるため使わない。
    /// - これは「ロジック専用」ストリーム。演出用乱数を混ぜてはならない
    ///   （演出の抽選が1回増えるだけで対戦結果が変わる事故を構造的に防ぐ）。
    ///   演出用が必要なら Fork() で独立した子ストリームを切り出す。
    /// - シードはロジックへの「入力」として扱う（0 は内部で別値に置換される点に注意）。
    /// </summary>
    public sealed class DeterministicRandom
    {
        /// <summary>乱数の内部状態。</summary>
        private uint _state;

        /// <summary>DeterministicRandom を生成する。</summary>
        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 2463534242u : seed;
        }

        /// <summary>内部状態を1回進めて乱数を返す。</summary>
        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>0-999 の一様乱数。（剰余バイアスは許容範囲として採用。厳密には棄却サンプリング）</summary>
        public int NextPermille()
        {
            return (int)(NextUInt() % 1000);
        }

        /// <summary>0 以上 max 未満。</summary>
        public int NextInt(int max)
        {
            return max <= 0 ? 0 : (int)(NextUInt() % (uint)max);
        }

        /// <summary>
        /// chancePermille‰ の確率判定。
        /// [規約] 確率 0‰ 以下 / 1000‰ 以上のときは乱数を消費しない。
        /// 「絶対に起きない/必ず起きる」判定の有無で乱数列がずれることを防ぎ、
        /// 消費数まで含めて決定的にする。
        /// </summary>
        public bool Roll(int chancePermille)
        {
            if (chancePermille <= 0)
            {
                return false;
            }
            if (chancePermille >= Permille.One)
            {
                return true;
            }
            return NextPermille() < chancePermille;
        }

        /// <summary>
        /// 常に乱数を1回消費する確率判定。
        /// ハンドラー補正で確率が動的に 0‰/1000‰ を跨ぐ仕様（命中率補正など）では、
        /// Roll だと消費回数が変わって乱数列がズレる。そうした判定にはこちらを使い、
        /// 「仕様ごとにどちらを使うか」を固定すること。
        /// </summary>
        public bool RollAlwaysConsume(int chancePermille)
        {
            var value = NextPermille();
            return value < chancePermille;
        }

        /// <summary>現在の内部状態を取得する（スナップショット・巻き戻し用）。</summary>
        public uint CaptureState()
        {
            return _state;
        }

        /// <summary>内部状態を復元する。CaptureState とペアで使う。</summary>
        public void RestoreState(uint state)
        {
            _state = state == 0 ? 2463534242u : state;
        }

        /// <summary>
        /// 独立した子ストリームを派生させる（親を1回消費し、撹拌して子のシードにする）。
        /// 「ユニットごとの乱数」「演出用の乱数」を親から決定的に切り出したいときに使う。
        /// </summary>
        public DeterministicRandom Fork()
        {
            // splitmix32 風の撹拌で親子の相関を断つ
            var x = NextUInt();
            x += 0x9E3779B9u;
            x ^= x >> 16;
            x *= 0x21F0AAADu;
            x ^= x >> 15;
            x *= 0x735A2D97u;
            x ^= x >> 15;
            return new DeterministicRandom(x == 0 ? 1u : x);
        }
    }
}
