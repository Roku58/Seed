using System;
using System.Collections.Generic;

namespace Seed.StageGen
{
    /// <summary>
    /// 部屋・廊下の手作りテンプレート（生成に織り込むセルパターンのスタンプ）。
    ///
    /// 文字列の行と凡例（文字→意味）で手書きできる:
    /// <code>
    ///   var legend = new RoomTemplateLegend()
    ///       .Cell('#', CellType.Wall)
    ///       .Cell('.', CellType.Floor)
    ///       .Door('D')
    ///       .Placement('C', CellType.Floor, PlacementKind.Chest, refId: 10);
    ///   var vault = RoomTemplate.Parse(new[] { "#####", "#C..#", "#...D", "#####" }, legend);
    /// </code>
    /// 配置物（宝箱・イベント）を内包できるため「宝物庫」を丸ごと1単位で登録できる。
    /// SO化（EntityDefinitionAsset 派生に行文字列を持たせる）すれば企画がInspectorで書ける。
    /// </summary>
    public sealed class RoomTemplate
    {
        /// <summary>テンプレート内の配置物（ローカル座標）。</summary>
        public readonly struct LocalPlacement
        {
            /// <summary>種別。</summary>
            public readonly PlacementKind Kind;

            /// <summary>参照ID。</summary>
            public readonly int RefId;

            /// <summary>ローカルX。</summary>
            public readonly int X;

            /// <summary>ローカルY。</summary>
            public readonly int Y;

            /// <summary>LocalPlacement を生成する。</summary>
            public LocalPlacement(PlacementKind kind, int refId, int x, int y)
            {
                Kind = kind;
                RefId = refId;
                X = x;
                Y = y;
            }
        }

        /// <summary>セルパターン（[y * Width + x]）。</summary>
        private readonly CellType[] _cells;

        /// <summary>RoomTemplate を生成する（Parse 推奨）。</summary>
        public RoomTemplate(int width, int height, CellType[] cells,
            List<LocalPlacement> placements, List<int> doorIndices)
        {
            Width = width;
            Height = height;
            _cells = cells;
            Placements = placements;
            DoorIndices = doorIndices;
        }

        /// <summary>幅。</summary>
        public int Width { get; }

        /// <summary>高さ。</summary>
        public int Height { get; }

        /// <summary>内包する配置物。</summary>
        public IReadOnlyList<LocalPlacement> Placements { get; }

        /// <summary>出入口セルのローカルインデックス（y * Width + x）。</summary>
        public IReadOnlyList<int> DoorIndices { get; }

        /// <summary>ローカル座標のセルを読む。</summary>
        public CellType Get(int x, int y)
        {
            return _cells[y * Width + x];
        }

        /// <summary>行文字列＋凡例からテンプレートを組み立てる（全行同じ長さであること）。</summary>
        public static RoomTemplate Parse(string[] rows, RoomTemplateLegend legend)
        {
            if (rows == null || rows.Length == 0)
            {
                throw new ArgumentException("テンプレートの行が空", nameof(rows));
            }
            var height = rows.Length;
            var width = rows[0].Length;
            var cells = new CellType[width * height];
            var placements = new List<LocalPlacement>(2);
            var doors = new List<int>(2);

            for (var y = 0; y < height; y++)
            {
                // 行文字列は上から書くのが自然なので、上の行ほど大きいYになるよう反転する
                var row = rows[height - 1 - y];
                if (row.Length != width)
                {
                    throw new ArgumentException($"行の長さが不揃い（{row}）", nameof(rows));
                }
                for (var x = 0; x < width; x++)
                {
                    var entry = legend.Resolve(row[x]);
                    var index = y * width + x;
                    cells[index] = entry.Cell;
                    if (entry.IsDoor)
                    {
                        doors.Add(index);
                    }
                    if (!entry.Placement.Equals(PlacementKind.None))
                    {
                        placements.Add(new LocalPlacement(entry.Placement, entry.RefId, x, y));
                    }
                }
            }
            return new RoomTemplate(width, height, cells, placements, doors);
        }
    }

    /// <summary>テンプレートの凡例（文字→セル種別・出入口・配置物）。</summary>
    public sealed class RoomTemplateLegend
    {
        /// <summary>凡例1件。</summary>
        public readonly struct Entry
        {
            /// <summary>セル種別。</summary>
            public readonly CellType Cell;

            /// <summary>出入口か。</summary>
            public readonly bool IsDoor;

            /// <summary>内包する配置物の種別（None=なし）。</summary>
            public readonly PlacementKind Placement;

            /// <summary>配置物の参照ID。</summary>
            public readonly int RefId;

            /// <summary>Entry を生成する。</summary>
            public Entry(CellType cell, bool isDoor, PlacementKind placement, int refId)
            {
                Cell = cell;
                IsDoor = isDoor;
                Placement = placement;
                RefId = refId;
            }
        }

        /// <summary>文字→意味の辞書。</summary>
        private readonly Dictionary<char, Entry> _entries = new Dictionary<char, Entry>();

        /// <summary>セル種別の凡例を足す。</summary>
        public RoomTemplateLegend Cell(char symbol, CellType cell)
        {
            _entries.Add(symbol, new Entry(cell, false, PlacementKind.None, 0));
            return this;
        }

        /// <summary>出入口（Doorセル・接続点）の凡例を足す。</summary>
        public RoomTemplateLegend Door(char symbol)
        {
            _entries.Add(symbol, new Entry(CellType.Door, true, PlacementKind.None, 0));
            return this;
        }

        /// <summary>配置物つきセル（宝箱など。下地セル＋配置）の凡例を足す。</summary>
        public RoomTemplateLegend Placement(char symbol, CellType underCell,
            PlacementKind kind, int refId)
        {
            _entries.Add(symbol, new Entry(underCell, false, kind, refId));
            return this;
        }

        /// <summary>文字を解決する（未定義は凡例の書き漏らしとして例外）。</summary>
        public Entry Resolve(char symbol)
        {
            if (_entries.TryGetValue(symbol, out var entry))
            {
                return entry;
            }
            throw new ArgumentException($"凡例に無い文字 '{symbol}'（Legend への登録漏れ）");
        }
    }
}
