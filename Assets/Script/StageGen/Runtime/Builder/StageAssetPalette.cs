using System.Collections.Generic;
using UnityEngine;

namespace Seed.StageGen
{
    /// <summary>タイル1個を建てる工場（プレハブの Instantiate でも、プリミティブ生成でもよい）。</summary>
    public delegate GameObject TileFactory(Vector3 position, float cellSize, Transform parent);

    /// <summary>
    /// アセットパレット（(バイオーム, セル種別, バリアント) → タイル工場の登録表）。
    ///
    /// 「床や壁などの複数素材登録」の施工側。検索はフォールバック連鎖:
    ///   (バイオーム, セル, バリアント) → (None, セル, バリアント) → (None, セル, 0) → 内蔵プリミティブ
    /// **アセットを1つも登録しなくても必ず建ち、登録した分だけ差し替わる**——
    /// プレハブが出来たら合成ルートの Bind 行を足すだけで、生成ロジックは一切変わらない。
    /// </summary>
    public sealed class StageAssetPalette
    {
        /// <summary>登録表。</summary>
        private readonly Dictionary<(int Biome, int Cell, int Variant), TileFactory> _factories =
            new Dictionary<(int, int, int), TileFactory>();

        /// <summary>内蔵プリミティブの色（セル種別→基本色。未登録種別の最終手段）。</summary>
        private readonly Dictionary<int, Color> _fallbackColors = new Dictionary<int, Color>
        {
            { CellType.Floor.Value, new Color(0.55f, 0.55f, 0.55f) },
            { CellType.Wall.Value, new Color(0.35f, 0.35f, 0.4f) },
            { CellType.Road.Value, new Color(0.45f, 0.42f, 0.38f) },
            { CellType.Building.Value, new Color(0.5f, 0.4f, 0.3f) },
            { CellType.Door.Value, new Color(0.7f, 0.6f, 0.35f) },
        };

        /// <summary>工場を登録する（バイオーム・バリアント指定つき）。流れるように書ける糖衣。</summary>
        public StageAssetPalette Bind(BiomeId biome, CellType cell, int variant, TileFactory factory)
        {
            _factories[(biome.Value, cell.Value, variant)] = factory;
            return this;
        }

        /// <summary>既定素材（バイオーム不問・バリアント0）を登録する。</summary>
        public StageAssetPalette Bind(CellType cell, TileFactory factory)
        {
            return Bind(BiomeId.None, cell, 0, factory);
        }

        /// <summary>内蔵プリミティブの色を差し替える（アプリ独自セル種別の最終手段の色）。</summary>
        public StageAssetPalette SetFallbackColor(CellType cell, Color color)
        {
            _fallbackColors[cell.Value] = color;
            return this;
        }

        /// <summary>フォールバック連鎖で工場を解決する（必ず何かを返す）。</summary>
        public TileFactory Resolve(BiomeId biome, CellType cell, int variant)
        {
            if (_factories.TryGetValue((biome.Value, cell.Value, variant), out var factory)
                || _factories.TryGetValue((BiomeId.None.Value, cell.Value, variant), out factory)
                || _factories.TryGetValue((BiomeId.None.Value, cell.Value, 0), out factory))
            {
                return factory;
            }
            // 内蔵プリミティブ（最終手段）: 壁系は箱・歩行可能系は薄板
            var color = _fallbackColors.TryGetValue(cell.Value, out var c)
                ? c
                : new Color(0.8f, 0.3f, 0.8f); // 未知セル種別はマゼンタで気づかせる
            return cell.IsWalkable
                ? PrimitiveTiles.FlatTile(color)
                : PrimitiveTiles.BlockTile(color, cell.Equals(CellType.Building) ? 3f : 2f);
        }
    }

    /// <summary>内蔵プリミティブのタイル工場（アセット未登録時の最終手段・デモ用）。</summary>
    public static class PrimitiveTiles
    {
        /// <summary>床・道路用の薄板タイル（BoxCollider つき＝地面になる）。</summary>
        public static TileFactory FlatTile(Color color, float thickness = 0.1f)
        {
            return (position, cellSize, parent) =>
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(parent, false);
                tile.transform.localPosition = position + new Vector3(0f, -thickness * 0.5f, 0f);
                tile.transform.localScale = new Vector3(cellSize, thickness, cellSize);
                Tint(tile, color);
                return tile;
            };
        }

        /// <summary>壁・建物用の箱タイル（BoxCollider つき＝移動を遮る）。</summary>
        public static TileFactory BlockTile(Color color, float height = 2f)
        {
            return (position, cellSize, parent) =>
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(parent, false);
                tile.transform.localPosition = position + new Vector3(0f, height * 0.5f, 0f);
                tile.transform.localScale = new Vector3(cellSize, height, cellSize);
                Tint(tile, color);
                return tile;
            };
        }

        /// <summary>配置マーカー用の目印タイル（薄い発色板。コライダーなし）。</summary>
        public static TileFactory MarkerTile(Color color)
        {
            return (position, cellSize, parent) =>
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(tile.GetComponent<Collider>()); // 通行を妨げない
                tile.transform.SetParent(parent, false);
                tile.transform.localPosition = position + new Vector3(0f, 0.06f, 0f);
                tile.transform.localScale = new Vector3(cellSize * 0.8f, 0.12f, cellSize * 0.8f);
                Tint(tile, color);
                return tile;
            };
        }

        /// <summary>MPBで色を上書きする（マテリアル複製を避ける。URP/Built-in両対応）。</summary>
        private static void Tint(GameObject tile, Color color)
        {
            var renderer = tile.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
