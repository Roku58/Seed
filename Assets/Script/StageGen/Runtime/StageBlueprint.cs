using System.Collections.Generic;
using UnityEngine;

namespace Seed.StageGen
{
    /// <summary>
    /// ステージの設計図（生成の出力・施工の入力。純データ）。
    ///
    /// 「設計図と施工の分離」の中心——地形3レイヤー・区画・配置物のすべてがここに
    /// 焼き込まれ、施工（StageBuilder）は一切の抽選をしない。
    /// 同じシード・設定なら設計図はバイト単位で同一＝ステージも決定的リプレイの一部になる。
    /// </summary>
    public sealed class StageBlueprint
    {
        /// <summary>StageBlueprint を生成する。</summary>
        public StageBlueprint(int width, int height, float cellSize, uint seed)
        {
            Grid = new StageGrid(width, height);
            CellSize = cellSize;
            Seed = seed;
        }

        /// <summary>地形グリッド（種別・バリアント・バイオーム）。</summary>
        public StageGrid Grid { get; }

        /// <summary>1セルのワールドサイズ（m）。</summary>
        public float CellSize { get; }

        /// <summary>生成に使ったシード（検証・再現用の記録）。</summary>
        public uint Seed { get; }

        /// <summary>区画（BSPの街区・スタンプ済みテンプレートの範囲）。</summary>
        public List<GridRect> Regions { get; } = new List<GridRect>(8);

        /// <summary>配置物（敵・出口・ショップ・宝箱…）。</summary>
        public List<Placement> Placements { get; } = new List<Placement>(16);

        /// <summary>配置物を追加する。</summary>
        public void AddPlacement(PlacementKind kind, int refId, int x, int y)
        {
            Placements.Add(new Placement(kind, refId, x, y));
        }

        /// <summary>そのセルに配置物が既にあるか（重なり防止）。</summary>
        public bool IsOccupied(int x, int y)
        {
            for (var i = 0; i < Placements.Count; i++)
            {
                if (Placements[i].X == x && Placements[i].Y == y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定種別の最初の配置物を探す。</summary>
        public bool TryFindPlacement(PlacementKind kind, out Placement placement)
        {
            for (var i = 0; i < Placements.Count; i++)
            {
                if (Placements[i].Kind.Equals(kind))
                {
                    placement = Placements[i];
                    return true;
                }
            }
            placement = default;
            return false;
        }

        /// <summary>グリッド座標→ワールド座標（ステージ中心が原点。y=0）。</summary>
        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(
                (x - (Grid.Width - 1) * 0.5f) * CellSize,
                0f,
                (y - (Grid.Height - 1) * 0.5f) * CellSize);
        }
    }
}
