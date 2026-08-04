using System.Collections.Generic;
using UnityEngine;

namespace Seed.StageGen
{
    /// <summary>配置物1件を実体化する工場（意味づけは呼び出し側＝アプリが持つ）。</summary>
    public delegate void PlacementFactory(in Placement placement, Vector3 worldPosition, Transform parent);

    /// <summary>
    /// 施工者（設計図 → GameObject群）。
    ///
    /// 設計図に焼き込まれた内容をそのまま建てるだけで、**一切の抽選をしない**
    /// （抽選は生成側で完了済み＝同じ設計図なら見た目まで同一）。
    /// - 地形: アセットパレットのフォールバック連鎖で必ず建つ（壁系はコライダーつき）
    /// - 配置物: SetPlacement の登録表で実体化。**未登録の種別は黙殺せず警告**
    ///   （レコード翻訳表と同じ規約——種別を増やして実体化を書き忘れた事故を検知する）
    /// </summary>
    public sealed class StageBuilder
    {
        /// <summary>地形のアセットパレット。</summary>
        private readonly StageAssetPalette _palette;

        /// <summary>配置物の実体化表。</summary>
        private readonly Dictionary<int, PlacementFactory> _placementFactories =
            new Dictionary<int, PlacementFactory>();

        /// <summary>StageBuilder を生成する（palette 省略時は内蔵プリミティブのみ）。</summary>
        public StageBuilder(StageAssetPalette palette = null)
        {
            _palette = palette ?? new StageAssetPalette();
        }

        /// <summary>配置物の実体化を登録する（流れるように書ける糖衣）。</summary>
        public StageBuilder SetPlacement(PlacementKind kind, PlacementFactory factory)
        {
            _placementFactories[kind.Value] = factory;
            return this;
        }

        /// <summary>設計図どおりに建てる（親の下へまとめる＝フェーズ退場時に丸ごと消せる）。</summary>
        public void Build(StageBlueprint blueprint, Transform parent)
        {
            var grid = blueprint.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = grid.Get(x, y);
                    if (cell.Equals(CellType.None))
                    {
                        continue;
                    }
                    var factory = _palette.Resolve(grid.GetBiome(x, y), cell, grid.GetVariant(x, y));
                    factory(blueprint.GridToWorld(x, y), blueprint.CellSize, parent);
                }
            }

            for (var i = 0; i < blueprint.Placements.Count; i++)
            {
                var placement = blueprint.Placements[i];
                if (_placementFactories.TryGetValue(placement.Kind.Value, out var factory))
                {
                    factory(in placement, blueprint.GridToWorld(placement.X, placement.Y), parent);
                }
                else
                {
                    Debug.LogWarning(
                        $"[StageBuilder] 実体化表に無い配置種別: {placement.Kind}（SetPlacement を追加すること）");
                }
            }
        }
    }
}
