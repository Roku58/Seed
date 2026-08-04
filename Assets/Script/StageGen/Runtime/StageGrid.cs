using System;
using System.Collections.Generic;

namespace Seed.StageGen
{
    /// <summary>
    /// 地形グリッド（3レイヤー: セル種別・素材バリアント・バイオーム）。
    ///
    /// バリアント・バイオームまでグリッドが持つのは「設計図が見た目まで含めて真実」の
    /// 規約のため——施工側で乱数を引かないので、同じシードなら見た目まで同一になる。
    /// BFS距離マップは配置パス（距離帯・最遠点）と連結性検査の共通道具としてここに置く。
    /// </summary>
    public sealed class StageGrid
    {
        /// <summary>セル種別。</summary>
        private readonly CellType[] _cells;

        /// <summary>素材バリアント番号。</summary>
        private readonly int[] _variants;

        /// <summary>バイオームID。</summary>
        private readonly int[] _biomes;

        /// <summary>StageGrid を生成する（全セル None・バリアント0・バイオームNone）。</summary>
        public StageGrid(int width, int height)
        {
            if (width < 3 || height < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "グリッドは3x3以上");
            }
            Width = width;
            Height = height;
            _cells = new CellType[width * height];
            _variants = new int[width * height];
            _biomes = new int[width * height];
        }

        /// <summary>幅（セル数）。</summary>
        public int Width { get; }

        /// <summary>高さ（セル数）。</summary>
        public int Height { get; }

        /// <summary>範囲内か。</summary>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>セル種別を読む。</summary>
        public CellType Get(int x, int y)
        {
            return _cells[y * Width + x];
        }

        /// <summary>セル種別を書く。</summary>
        public void Set(int x, int y, CellType cell)
        {
            _cells[y * Width + x] = cell;
        }

        /// <summary>バリアント番号を読む。</summary>
        public int GetVariant(int x, int y)
        {
            return _variants[y * Width + x];
        }

        /// <summary>バリアント番号を書く。</summary>
        public void SetVariant(int x, int y, int variant)
        {
            _variants[y * Width + x] = variant;
        }

        /// <summary>バイオームを読む。</summary>
        public BiomeId GetBiome(int x, int y)
        {
            return new BiomeId(_biomes[y * Width + x]);
        }

        /// <summary>バイオームを書く。</summary>
        public void SetBiome(int x, int y, BiomeId biome)
        {
            _biomes[y * Width + x] = biome.Value;
        }

        /// <summary>全セルを指定種別で埋める。</summary>
        public void Fill(CellType cell)
        {
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = cell;
            }
        }

        /// <summary>指定種別のセル数を数える。</summary>
        public int CountOf(CellType cell)
        {
            var count = 0;
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Equals(cell))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>最初の歩行可能セルを走査順で探す。</summary>
        public bool TryFindFirstWalkable(out int x, out int y)
        {
            for (var yy = 0; yy < Height; yy++)
            {
                for (var xx = 0; xx < Width; xx++)
                {
                    if (Get(xx, yy).IsWalkable)
                    {
                        x = xx;
                        y = yy;
                        return true;
                    }
                }
            }
            x = 0;
            y = 0;
            return false;
        }

        /// <summary>
        /// 起点からの歩行可能セルのBFS距離マップを返す（到達不能・非歩行は -1）。
        /// 配置の距離帯・最遠点・連結性検査の共通道具。走査順固定＝決定的。
        /// </summary>
        public int[] ComputeDistances(int startX, int startY)
        {
            var distances = new int[Width * Height];
            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = -1;
            }
            if (!InBounds(startX, startY) || !Get(startX, startY).IsWalkable)
            {
                return distances;
            }

            var queue = new Queue<int>();
            distances[startY * Width + startX] = 0;
            queue.Enqueue(startY * Width + startX);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % Width;
                var y = index / Width;
                var next = distances[index] + 1;
                Visit(x + 1, y, next, distances, queue);
                Visit(x - 1, y, next, distances, queue);
                Visit(x, y + 1, next, distances, queue);
                Visit(x, y - 1, next, distances, queue);
            }
            return distances;
        }

        /// <summary>BFSの1近傍を処理する。</summary>
        private void Visit(int x, int y, int distance, int[] distances, Queue<int> queue)
        {
            if (!InBounds(x, y) || !Get(x, y).IsWalkable)
            {
                return;
            }
            var index = y * Width + x;
            if (distances[index] >= 0)
            {
                return;
            }
            distances[index] = distance;
            queue.Enqueue(index);
        }
    }
}
