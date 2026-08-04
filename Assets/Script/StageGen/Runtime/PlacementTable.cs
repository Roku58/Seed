using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 出現テーブル（参照ID＋重み‰の行列。敵の種類・宝箱の中身などの抽選に使う）。
    /// 抽選は DeterministicRandom で決定的。行の意味（refId が何のIDか）はアプリが決める。
    /// </summary>
    public sealed class PlacementTable
    {
        /// <summary>テーブル1行。</summary>
        private readonly struct Row
        {
            /// <summary>参照ID。</summary>
            public readonly int RefId;

            /// <summary>重み（‰でなくてもよい相対値）。</summary>
            public readonly int Weight;

            /// <summary>Row を生成する。</summary>
            public Row(int refId, int weight)
            {
                RefId = refId;
                Weight = weight;
            }
        }

        /// <summary>行（登録順）。</summary>
        private readonly List<Row> _rows = new List<Row>(4);

        /// <summary>重みの合計。</summary>
        private int _totalWeight;

        /// <summary>行数。</summary>
        public int Count => _rows.Count;

        /// <summary>行を足す（重み0以下は「絶対に出ない」として無視）。流れるように書ける糖衣。</summary>
        public PlacementTable Add(int refId, int weight)
        {
            if (weight > 0)
            {
                _rows.Add(new Row(refId, weight));
                _totalWeight += weight;
            }
            return this;
        }

        /// <summary>重み付き抽選で参照IDを1つ選ぶ（空テーブルは0）。</summary>
        public int Pick(DeterministicRandom random)
        {
            if (_totalWeight <= 0)
            {
                return 0;
            }
            var roll = random.NextInt(_totalWeight);
            for (var i = 0; i < _rows.Count; i++)
            {
                roll -= _rows[i].Weight;
                if (roll < 0)
                {
                    return _rows[i].RefId;
                }
            }
            return _rows[_rows.Count - 1].RefId; // 端数の保険（理論上到達しない）
        }
    }
}
