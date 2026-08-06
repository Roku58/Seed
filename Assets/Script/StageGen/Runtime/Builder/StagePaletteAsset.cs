using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seed.StageGen
{
    /// <summary>
    /// セル種別・配置種別の表示名（エディタ表示と実行時の警告文で共用する）。
    /// 基盤が予約している番号だけを知っており、アプリ独自の番号は番号のまま返す。
    /// </summary>
    public static class StageGenNames
    {
        /// <summary>セル種別の表示名。</summary>
        public static string Cell(int value)
        {
            switch (value)
            {
                case 0: return "None";
                case 1: return "Floor（床）";
                case 2: return "Wall（壁）";
                case 3: return "Road（道路）";
                case 4: return "Building（建物）";
                case 5: return "Door（出入口）";
                default: return $"Cell#{value}（アプリ独自）";
            }
        }

        /// <summary>配置種別の表示名。</summary>
        public static string Placement(int value)
        {
            switch (value)
            {
                case 0: return "None";
                case 1: return "PlayerSpawn（開始地点）";
                case 2: return "EnemySpawn（敵の出現）";
                case 3: return "Exit（出口）";
                case 4: return "Shop（店）";
                case 5: return "Chest（宝箱）";
                case 6: return "EventTrigger（イベント）";
                default: return $"Placement#{value}（アプリ独自）";
            }
        }

        /// <summary>バイオームの表示名（値の意味はアプリ定義なので番号で示す）。</summary>
        public static string Biome(int value)
        {
            return value == 0 ? "共通（バイオーム不問）" : $"Biome#{value}";
        }
    }

    /// <summary>
    /// ステージ生成で使うアセットの紐付け表（ScriptableObject）。
    ///
    /// [何のためにあるか] 生成された設計図（どのセルが床か・どこに宝箱を置くか）を
    /// 実際の見た目にするには「役割 → プレハブ」の対応が必要になる。それをコードの
    /// <c>Bind</c> 行で持つと、美術アセットが増えるたびにコードを触ることになる。
    /// この資産に紐付けを保存しておけば、**アセットの差し替えはエディタ操作だけで済み、
    /// 生成ロジックにも合成ルートにも触らない**。
    ///
    /// [生成の決定性を壊さない] ここで持つのは「役割にどのプレハブを使うか」だけで、
    /// 抽選は一切しない（バリアントの抽選は生成側で設計図へ焼き込み済み）。
    /// 施工で乱数を引くと「同じシードなのに見た目が違う」が起き、リプレイと矛盾するためである。
    ///
    /// [使い方] 実行時は <see cref="BuildPalette"/> と <see cref="ApplyPlacements"/> を
    /// 呼んで <see cref="StageAssetPalette"/> / <see cref="StageBuilder"/> へ流し込む。
    /// 未登録の役割は内蔵プリミティブへフォールバックするので、**途中まで埋めた状態でも動く**。
    /// </summary>
    [CreateAssetMenu(menuName = "Seed/Stage Palette", fileName = "StagePalette")]
    public sealed class StagePaletteAsset : ScriptableObject
    {
        /// <summary>地形タイル1行ぶんの紐付け。</summary>
        [Serializable]
        public sealed class TileEntry
        {
            /// <summary>バイオームID値（0＝バイオーム不問の既定素材）。</summary>
            public int Biome;

            /// <summary>セル種別のID値（床・壁など）。</summary>
            public int Cell = 1;

            /// <summary>バリアント番号（同じ役割の素材違い。0が既定）。</summary>
            public int Variant;

            /// <summary>使用するプレハブ（未設定なら内蔵プリミティブへフォールバック）。</summary>
            public GameObject Prefab;

            /// <summary>設置の高さ調整（m）。原点が足元でないモデルの補正に使う。</summary>
            public float YOffset;

            /// <summary>セルの大きさに合わせて拡縮するか（1×1想定のプレハブに使う）。</summary>
            public bool ScaleToCell;
        }

        /// <summary>配置物1行ぶんの紐付け。</summary>
        [Serializable]
        public sealed class PlacementEntry
        {
            /// <summary>配置種別のID値（出口・店・宝箱など）。</summary>
            public int Kind = 1;

            /// <summary>使用するプレハブ（未設定なら実行時に警告が出る）。</summary>
            public GameObject Prefab;

            /// <summary>設置の高さ調整（m）。</summary>
            public float YOffset;
        }

        /// <summary>地形タイルの紐付け一覧。</summary>
        [SerializeField]
        private List<TileEntry> _tiles = new List<TileEntry>();

        /// <summary>配置物の紐付け一覧。</summary>
        [SerializeField]
        private List<PlacementEntry> _placements = new List<PlacementEntry>();

        /// <summary>地形タイルの紐付け一覧（エディタからの編集用）。</summary>
        public List<TileEntry> Tiles => _tiles;

        /// <summary>配置物の紐付け一覧（エディタからの編集用）。</summary>
        public List<PlacementEntry> Placements => _placements;

        /// <summary>
        /// 施工用のパレットを組む（実行時に1回。プレハブ未設定の行は飛ばす）。
        /// </summary>
        public StageAssetPalette BuildPalette()
        {
            var palette = new StageAssetPalette();
            for (var i = 0; i < _tiles.Count; i++)
            {
                var entry = _tiles[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }
                palette.Bind(new BiomeId(entry.Biome), new CellType(entry.Cell), entry.Variant,
                    CreateTileFactory(entry.Prefab, entry.YOffset, entry.ScaleToCell));
            }
            return palette;
        }

        /// <summary>配置物の実体化を施工者へ登録する（プレハブ未設定の行は飛ばす）。</summary>
        public void ApplyPlacements(StageBuilder builder)
        {
            for (var i = 0; i < _placements.Count; i++)
            {
                var entry = _placements[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }
                builder.SetPlacement(new PlacementKind(entry.Kind),
                    CreatePlacementFactory(entry.Prefab, entry.YOffset));
            }
        }

        /// <summary>
        /// 紐付けの不備を洗い出す（重複キー・プレハブ未設定・不正な番号）。
        /// 空リストなら合格。エディタの検証ボタンと実行時の事前チェックで共用する。
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var tileKeys = new HashSet<(int, int, int)>();
            for (var i = 0; i < _tiles.Count; i++)
            {
                var entry = _tiles[i];
                if (entry == null)
                {
                    continue;
                }
                var label = $"タイル行{i + 1}（{StageGenNames.Biome(entry.Biome)} / " +
                    $"{StageGenNames.Cell(entry.Cell)} / バリアント{entry.Variant}）";
                if (entry.Cell <= 0)
                {
                    problems.Add($"{label}: セル種別が未設定（1以上を指定）");
                }
                if (entry.Variant < 0)
                {
                    problems.Add($"{label}: バリアント番号が負値");
                }
                if (entry.Prefab == null)
                {
                    problems.Add($"{label}: プレハブ未設定（内蔵プリミティブで代替されます）");
                }
                if (!tileKeys.Add((entry.Biome, entry.Cell, entry.Variant)))
                {
                    problems.Add($"{label}: 同じ組み合わせが重複（後の行で上書きされ、前の行は無効）");
                }
            }

            var placementKinds = new HashSet<int>();
            for (var i = 0; i < _placements.Count; i++)
            {
                var entry = _placements[i];
                if (entry == null)
                {
                    continue;
                }
                var label = $"配置行{i + 1}（{StageGenNames.Placement(entry.Kind)}）";
                if (entry.Kind <= 0)
                {
                    problems.Add($"{label}: 配置種別が未設定（1以上を指定）");
                }
                if (entry.Prefab == null)
                {
                    problems.Add($"{label}: プレハブ未設定（実行時に施工されず警告が出ます）");
                }
                if (!placementKinds.Add(entry.Kind))
                {
                    problems.Add($"{label}: 同じ配置種別が重複（後の行で上書きされます）");
                }
            }
            return problems;
        }

        /// <summary>プレハブを建てる工場を作る（内蔵プリミティブと同じ親基準の座標系で置く）。</summary>
        private static TileFactory CreateTileFactory(GameObject prefab, float yOffset, bool scaleToCell)
        {
            return (position, cellSize, parent) =>
            {
                var tile = Instantiate(prefab, parent);
                tile.name = prefab.name;
                var transform = tile.transform;
                transform.localPosition = position + new Vector3(0f, yOffset, 0f);
                if (scaleToCell)
                {
                    transform.localScale = new Vector3(cellSize, transform.localScale.y, cellSize);
                }
                return tile;
            };
        }

        /// <summary>配置物を建てる工場を作る。</summary>
        private static PlacementFactory CreatePlacementFactory(GameObject prefab, float yOffset)
        {
            return (in Placement placement, Vector3 worldPosition, Transform parent) =>
            {
                var instance = Instantiate(prefab, parent);
                instance.name = prefab.name;
                instance.transform.localPosition = worldPosition + new Vector3(0f, yOffset, 0f);
            };
        }
    }
}
