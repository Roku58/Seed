using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 手作りの部屋テンプレートを生成へ織り込む（廊下や部屋などの単位での登録）。
    ///
    /// 各テンプレートを重ならない位置へ乱択スタンプし（試行上限つき・決定的）、
    /// セルと内包配置物（宝箱など）を設計図へ書き込む。スタンプ範囲は Regions に記録。
    /// 出入口（Door）は外側へ向けて直進の廊下を掘り、既存の歩行可能領域へ**必ず開口**する
    /// ——完全ランダム生成の中に「意図した部屋」が混ざっても全域連結が保たれる。
    /// </summary>
    public sealed class TemplateRoomsPass : IGenerationPass
    {
        /// <summary>スタンプするテンプレートの列（この順に配置を試みる）。</summary>
        private readonly IReadOnlyList<RoomTemplate> _templates;

        /// <summary>1テンプレートあたりの配置試行上限。</summary>
        private readonly int _attemptsPerTemplate;

        /// <summary>TemplateRoomsPass を生成する。</summary>
        public TemplateRoomsPass(IReadOnlyList<RoomTemplate> templates, int attemptsPerTemplate = 20)
        {
            _templates = templates;
            _attemptsPerTemplate = attemptsPerTemplate < 1 ? 1 : attemptsPerTemplate;
        }

        /// <summary>パス名。</summary>
        public string Name => "TemplateRooms";

        /// <summary>テンプレートをスタンプし、分断された領域を修復する。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            for (var i = 0; i < _templates.Count; i++)
            {
                TryStamp(blueprint, _templates[i], random);
            }
            // スタンプは既存の通路を上書きするため、迷路の一部が孤立することがある。
            // 島を検出して薄い壁を掘り、全域連結を必ず回復する（決定的な走査順）
            ReconnectIslands(blueprint.Grid);
        }

        /// <summary>1テンプレートの配置を試みる（置けなければ静かに諦める＝生成は失敗しない）。</summary>
        private void TryStamp(StageBlueprint blueprint, RoomTemplate template, DeterministicRandom random)
        {
            var grid = blueprint.Grid;
            var maxX = grid.Width - 1 - template.Width;
            var maxY = grid.Height - 1 - template.Height;
            if (maxX < 1 || maxY < 1)
            {
                return; // テンプレートがグリッドに入らない
            }

            for (var attempt = 0; attempt < _attemptsPerTemplate; attempt++)
            {
                var x = 1 + random.NextInt(maxX);
                var y = 1 + random.NextInt(maxY);
                var rect = new GridRect(x, y, template.Width, template.Height);

                // 既存の区画（他テンプレ・街区）とは余白1で重ねない
                var overlaps = false;
                for (var i = 0; i < blueprint.Regions.Count && !overlaps; i++)
                {
                    overlaps = rect.Intersects(blueprint.Regions[i], padding: 1);
                }
                if (overlaps)
                {
                    continue;
                }

                Stamp(blueprint, template, rect);
                return;
            }
        }

        /// <summary>スタンプ本体（セル・配置物・出入口の開口）。</summary>
        private static void Stamp(StageBlueprint blueprint, RoomTemplate template, GridRect rect)
        {
            var grid = blueprint.Grid;
            for (var y = 0; y < template.Height; y++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    grid.Set(rect.X + x, rect.Y + y, template.Get(x, y));
                }
            }
            for (var i = 0; i < template.Placements.Count; i++)
            {
                var local = template.Placements[i];
                blueprint.AddPlacement(local.Kind, local.RefId, rect.X + local.X, rect.Y + local.Y);
            }
            blueprint.Regions.Add(rect);

            // 出入口: テンプレ外周の Door から外方向へ直進で掘り、既存の歩行可能セルへ繋ぐ
            for (var i = 0; i < template.DoorIndices.Count; i++)
            {
                var index = template.DoorIndices[i];
                var doorX = rect.X + index % template.Width;
                var doorY = rect.Y + index / template.Width;
                CarveToWalkable(grid, doorX, doorY,
                    OutwardX(index % template.Width, template.Width),
                    OutwardY(index / template.Width, template.Height));
            }
        }

        /// <summary>Doorの位置から外向きのX方向（左右端でなければ0）。</summary>
        private static int OutwardX(int localX, int width)
        {
            if (localX == 0) { return -1; }
            return localX == width - 1 ? 1 : 0;
        }

        /// <summary>Doorの位置から外向きのY方向（上下端でなければ0）。</summary>
        private static int OutwardY(int localY, int height)
        {
            if (localY == 0) { return -1; }
            return localY == height - 1 ? 1 : 0;
        }

        /// <summary>外方向へ床を掘り進め、既存の歩行可能セルに当たったら止まる（開口の保証）。</summary>
        private static void CarveToWalkable(StageGrid grid, int doorX, int doorY, int dx, int dy)
        {
            if (dx == 0 && dy == 0)
            {
                return; // 内側のDoor（テンプレ設計の自由。開口不要）
            }
            var x = doorX + dx;
            var y = doorY + dy;
            while (x >= 1 && x < grid.Width - 1 && y >= 1 && y < grid.Height - 1)
            {
                if (grid.Get(x, y).IsWalkable)
                {
                    return; // 既存の通路へ到達
                }
                grid.Set(x, y, CellType.Floor);
                x += dx;
                y += dy;
            }
        }

        /// <summary>孤立した歩行可能領域（島）を、薄い壁を掘って本土へ繋ぎ直す。</summary>
        private static void ReconnectIslands(StageGrid grid)
        {
            for (var guard = 0; guard < 64; guard++)
            {
                var labels = LabelComponents(grid, out var componentCount);
                if (componentCount <= 1)
                {
                    return; // 全域連結
                }
                if (!CarveBridge(grid, labels))
                {
                    return; // これ以上繋げない（厚さ3超の分断は設計上発生しない）
                }
            }
        }

        /// <summary>歩行可能セルの連結成分を番号づけする。</summary>
        private static int[] LabelComponents(StageGrid grid, out int componentCount)
        {
            var labels = new int[grid.Width * grid.Height];
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = -1;
            }
            componentCount = 0;
            var queue = new Queue<int>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid.Get(x, y).IsWalkable || labels[y * grid.Width + x] >= 0)
                    {
                        continue;
                    }
                    var label = componentCount++;
                    labels[y * grid.Width + x] = label;
                    queue.Enqueue(y * grid.Width + x);
                    while (queue.Count > 0)
                    {
                        var index = queue.Dequeue();
                        var cx = index % grid.Width;
                        var cy = index / grid.Width;
                        Label(grid, labels, queue, cx + 1, cy, label);
                        Label(grid, labels, queue, cx - 1, cy, label);
                        Label(grid, labels, queue, cx, cy + 1, label);
                        Label(grid, labels, queue, cx, cy - 1, label);
                    }
                }
            }
            return labels;
        }

        /// <summary>連結成分ラベリングの1近傍。</summary>
        private static void Label(StageGrid grid, int[] labels, Queue<int> queue, int x, int y, int label)
        {
            if (!grid.InBounds(x, y) || !grid.Get(x, y).IsWalkable
                || labels[y * grid.Width + x] >= 0)
            {
                return;
            }
            labels[y * grid.Width + x] = label;
            queue.Enqueue(y * grid.Width + x);
        }

        /// <summary>
        /// 異なる成分を隔てる壁（厚さ1〜3）を1本掘って橋を架ける。
        /// 走査順・方向順が固定なので結果は決定的。
        /// </summary>
        private static bool CarveBridge(StageGrid grid, int[] labels)
        {
            var dx = new[] { 1, -1, 0, 0 };
            var dy = new[] { 0, 0, 1, -1 };
            for (var y = 1; y < grid.Height - 1; y++)
            {
                for (var x = 1; x < grid.Width - 1; x++)
                {
                    if (grid.Get(x, y).IsWalkable)
                    {
                        continue; // 壁セルだけが橋の起点
                    }
                    for (var direction = 0; direction < 4; direction++)
                    {
                        // 片側の床（成分A）
                        var ax = x - dx[direction];
                        var ay = y - dy[direction];
                        if (!grid.InBounds(ax, ay) || !grid.Get(ax, ay).IsWalkable)
                        {
                            continue;
                        }
                        var labelA = labels[ay * grid.Width + ax];

                        // 前方の壁を最大3セルまで貫き、別成分の床に届くか調べる
                        for (var length = 1; length <= 3; length++)
                        {
                            var fx = x + dx[direction] * length;
                            var fy = y + dy[direction] * length;
                            if (!grid.InBounds(fx, fy))
                            {
                                break;
                            }
                            if (!grid.Get(fx, fy).IsWalkable)
                            {
                                continue;
                            }
                            if (labels[fy * grid.Width + fx] == labelA)
                            {
                                break; // 同じ成分に戻るだけの掘削はしない
                            }
                            // 橋を架ける（壁 length セルを床に）
                            for (var step = 0; step < length; step++)
                            {
                                grid.Set(x + dx[direction] * step, y + dy[direction] * step,
                                    CellType.Floor);
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
