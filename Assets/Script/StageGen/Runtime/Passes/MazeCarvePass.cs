using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 迷路を掘る（穴掘り法＝再帰的バックトラッカーのスタック実装）。
    ///
    /// アルゴリズム:
    /// 1. 全面 Wall の下地（FillPass(Wall) を先に流す）に対し、(1,1) から開始
    /// 2. ランダム順で4方向を試し「2セル先が未掘りなら、間の壁ごと掘って前進」
    /// 3. 掘れる方向が無ければスタックで後退。空になれば完成
    /// → 全域連結・閉路なしの「完全迷路」（どの床からどの床へも必ず到達できる）
    ///
    /// braidPermille（‰）: 完成後、行き止まりの一部を貫通させて閉路を作る。
    /// 完全迷路は一本道の追い掛けっこになりがちなので、戦闘用には逃げ道を開ける。
    /// グリッドは奇数サイズを推奨（偶数でも動くが外周側に太い壁が残る）。
    /// </summary>
    public sealed class MazeCarvePass : IGenerationPass
    {
        /// <summary>行き止まり貫通の確率（‰）。</summary>
        private readonly int _braidPermille;

        /// <summary>MazeCarvePass を生成する。</summary>
        public MazeCarvePass(int braidPermille = 0)
        {
            _braidPermille = braidPermille;
        }

        /// <summary>パス名。</summary>
        public string Name => "MazeCarve";

        /// <summary>迷路を掘る。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            Carve(grid, random);
            if (_braidPermille > 0)
            {
                Braid(grid, random);
            }
        }

        /// <summary>穴掘り法の本体。</summary>
        private static void Carve(StageGrid grid, DeterministicRandom random)
        {
            var stack = new Stack<(int X, int Y)>();
            grid.Set(1, 1, CellType.Floor);
            stack.Push((1, 1));

            // 4方向（2セル先へ掘る）。順番はその場でランダムに並べ替える
            var dx = new[] { 1, -1, 0, 0 };
            var dy = new[] { 0, 0, 1, -1 };
            var order = new int[4];

            while (stack.Count > 0)
            {
                var (x, y) = stack.Peek();

                // 0..3 をランダム順に（Fisher-Yates。DeterministicRandom のみ使用）
                for (var i = 0; i < 4; i++)
                {
                    order[i] = i;
                }
                for (var i = 3; i > 0; i--)
                {
                    var j = random.NextInt(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                var advanced = false;
                for (var i = 0; i < 4 && !advanced; i++)
                {
                    var direction = order[i];
                    var nx = x + dx[direction] * 2;
                    var ny = y + dy[direction] * 2;
                    // 外周1セルは壁として残す
                    if (nx < 1 || nx >= grid.Width - 1 || ny < 1 || ny >= grid.Height - 1)
                    {
                        continue;
                    }
                    if (grid.Get(nx, ny).IsWalkable)
                    {
                        continue; // 掘削済み
                    }
                    grid.Set(x + dx[direction], y + dy[direction], CellType.Floor); // 間の壁
                    grid.Set(nx, ny, CellType.Floor);
                    stack.Push((nx, ny));
                    advanced = true;
                }
                if (!advanced)
                {
                    stack.Pop(); // 後退
                }
            }
        }

        /// <summary>行き止まりの一部を貫通させて閉路を作る。</summary>
        private void Braid(StageGrid grid, DeterministicRandom random)
        {
            var dx = new[] { 1, -1, 0, 0 };
            var dy = new[] { 0, 0, 1, -1 };
            for (var y = 1; y < grid.Height - 1; y++)
            {
                for (var x = 1; x < grid.Width - 1; x++)
                {
                    if (!grid.Get(x, y).IsWalkable || !IsDeadEnd(grid, x, y))
                    {
                        continue;
                    }
                    if (!random.Roll(_braidPermille))
                    {
                        continue;
                    }
                    // 「壁の向こうが床」の壁を1枚選んで抜く（外周は抜かない）
                    for (var i = 0; i < 4; i++)
                    {
                        var wx = x + dx[i];
                        var wy = y + dy[i];
                        var fx = x + dx[i] * 2;
                        var fy = y + dy[i] * 2;
                        if (fx < 1 || fx >= grid.Width - 1 || fy < 1 || fy >= grid.Height - 1)
                        {
                            continue;
                        }
                        if (!grid.Get(wx, wy).IsWalkable && grid.Get(fx, fy).IsWalkable)
                        {
                            grid.Set(wx, wy, CellType.Floor);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>行き止まり（歩行可能な隣が1つ以下）か。</summary>
        private static bool IsDeadEnd(StageGrid grid, int x, int y)
        {
            var open = 0;
            if (grid.Get(x + 1, y).IsWalkable) { open++; }
            if (grid.Get(x - 1, y).IsWalkable) { open++; }
            if (grid.Get(x, y + 1).IsWalkable) { open++; }
            if (grid.Get(x, y - 1).IsWalkable) { open++; }
            return open <= 1;
        }
    }
}
