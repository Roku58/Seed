namespace Seed.StageGen
{
    /// <summary>
    /// グリッド上の矩形（区画・テンプレートの範囲）。
    /// UnityEngine.RectInt を使わないのは生成コアの純度維持のため。
    /// </summary>
    public readonly struct GridRect
    {
        /// <summary>左下X。</summary>
        public readonly int X;

        /// <summary>左下Y。</summary>
        public readonly int Y;

        /// <summary>幅。</summary>
        public readonly int Width;

        /// <summary>高さ。</summary>
        public readonly int Height;

        /// <summary>GridRect を生成する。</summary>
        public GridRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>右端（含まない）。</summary>
        public int XMax => X + Width;

        /// <summary>上端（含まない）。</summary>
        public int YMax => Y + Height;

        /// <summary>中心X。</summary>
        public int CenterX => X + Width / 2;

        /// <summary>中心Y。</summary>
        public int CenterY => Y + Height / 2;

        /// <summary>点を含むか。</summary>
        public bool Contains(int x, int y)
        {
            return x >= X && x < XMax && y >= Y && y < YMax;
        }

        /// <summary>他の矩形と重なるか（padding で余白込みの判定）。</summary>
        public bool Intersects(in GridRect other, int padding = 0)
        {
            return X - padding < other.XMax && other.X < XMax + padding
                && Y - padding < other.YMax && other.Y < YMax + padding;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"({X},{Y} {Width}x{Height})";
        }
    }
}
