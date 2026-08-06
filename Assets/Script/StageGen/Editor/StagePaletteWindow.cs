using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Seed.StageGen.Editor
{
    /// <summary>
    /// ステージ生成アセットのセットアップ・ウィンドウ（Seed/Stage Palette）。
    ///
    /// 自動生成で使うプレハブと「役割」（バイオーム・セル種別・バリアント・配置種別）の
    /// 結び付けを、コードを触らずに作れる場所。生成ロジックは設計図しか知らないので、
    /// ここでの紐付けを差し替えるだけで見た目が丸ごと入れ替わる。
    ///
    /// 提供するもの:
    /// - 紐付けの一覧・追加・削除（プレハブは D&amp;D 可能な参照欄）
    /// - **未登録の役割の提示**（内蔵プリミティブで代替される箇所が Play 前に分かる）
    /// - **不備の検証**（キーの重複＝後の行に上書きされて無効になる罠、プレハブ未設定）
    /// - 定番の役割を一括で行追加する「雛形を並べる」
    ///
    /// 資産（<see cref="StagePaletteAsset"/>）は通常の ScriptableObject なので、
    /// このウィンドウを使わず Inspector で編集してもよい。
    /// </summary>
    public sealed class StagePaletteWindow : EditorWindow
    {
        /// <summary>基盤が予約しているセル種別（雛形と未登録チェックに使う）。</summary>
        private static readonly int[] KnownCells = { 1, 2, 3, 4, 5 };

        /// <summary>基盤が予約している配置種別。</summary>
        private static readonly int[] KnownPlacements = { 1, 2, 3, 4, 5, 6 };

        /// <summary>編集対象の資産。</summary>
        private StagePaletteAsset _asset;

        /// <summary>一覧のスクロール位置。</summary>
        private Vector2 _scroll;

        /// <summary>検証結果。</summary>
        private List<string> _problems = new List<string>();

        /// <summary>ウィンドウを開く。</summary>
        [MenuItem("Seed/Stage Palette")]
        public static void Open()
        {
            var window = GetWindow<StagePaletteWindow>("Stage Palette");
            window.minSize = new Vector2(460f, 320f);
        }

        /// <summary>フォーカスのたびに対象を探し直し、検証もかけ直す。</summary>
        private void OnFocus()
        {
            if (_asset == null)
            {
                _asset = FindFirstAsset();
            }
            Validate();
        }

        /// <summary>プロジェクト内の最初のパレット資産を探す（無ければ null）。</summary>
        private static StagePaletteAsset FindFirstAsset()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(StagePaletteAsset));
            if (guids.Length == 0)
            {
                return null;
            }
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<StagePaletteAsset>(path);
        }

        /// <summary>検証をかけ直す。</summary>
        private void Validate()
        {
            _problems = _asset != null ? _asset.Validate() : new List<string>();
        }

        /// <summary>ウィンドウ全体を描画する。</summary>
        private void OnGUI()
        {
            DrawToolbar();
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "編集するパレット資産がありません。「新規作成」で作るか、"
                    + "Project ビューで StagePalette を選んで上の欄へ入れてください。",
                    MessageType.Info);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                DrawMissingRoles();
                DrawProblems();
                DrawTiles();
                DrawPlacements();
            }
        }

        /// <summary>ツールバー（対象選択・新規作成・検証・保存）。</summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = (StagePaletteAsset)EditorGUILayout.ObjectField(
                    _asset, typeof(StagePaletteAsset), false, GUILayout.MinWidth(160f));
                if (next != _asset)
                {
                    _asset = next;
                    Validate();
                }
                if (GUILayout.Button("新規作成", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    CreateAsset();
                }
                if (GUILayout.Button("検証", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                {
                    Validate();
                    ReportToConsole();
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_asset == null))
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                    {
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        /// <summary>新しいパレット資産を作って選択する。</summary>
        private void CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "ステージパレットの作成", "StagePalette", "asset",
                "生成ステージで使うプレハブの紐付けを保存する資産を作ります");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var asset = CreateInstance<StagePaletteAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _asset = asset;
            Validate();
        }

        /// <summary>検証結果を Console にも出す（バッチ確認・共有用）。</summary>
        private void ReportToConsole()
        {
            if (_problems.Count == 0)
            {
                Debug.Log($"[StagePalette] 検証OK（タイル {_asset.Tiles.Count} 行・"
                    + $"配置 {_asset.Placements.Count} 行）");
                return;
            }
            for (var i = 0; i < _problems.Count; i++)
            {
                Debug.LogWarning($"[StagePalette] {_problems[i]}");
            }
        }

        /// <summary>未登録の役割（内蔵プリミティブ・警告で代替される箇所）を示す。</summary>
        private void DrawMissingRoles()
        {
            var missingCells = new List<string>();
            for (var i = 0; i < KnownCells.Length; i++)
            {
                if (!HasTile(KnownCells[i]))
                {
                    missingCells.Add(StageGenNames.Cell(KnownCells[i]));
                }
            }
            var missingPlacements = new List<string>();
            for (var i = 0; i < KnownPlacements.Length; i++)
            {
                if (!HasPlacement(KnownPlacements[i]))
                {
                    missingPlacements.Add(StageGenNames.Placement(KnownPlacements[i]));
                }
            }

            if (missingCells.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "プレハブ未登録の地形（内蔵プリミティブで建ちます）: "
                    + string.Join(" / ", missingCells), MessageType.Info);
            }
            if (missingPlacements.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "プレハブ未登録の配置物（実行時に警告が出ます。使わない種別なら無視してよい）: "
                    + string.Join(" / ", missingPlacements), MessageType.Info);
            }
            if (GUILayout.Button("定番の役割の雛形を並べる（未登録ぶんだけ行を追加）"))
            {
                AddTemplateRows();
            }
        }

        /// <summary>指定セル種別の行があるか（バイオーム不問・バリアント0の既定素材を見る）。</summary>
        private bool HasTile(int cell)
        {
            for (var i = 0; i < _asset.Tiles.Count; i++)
            {
                var entry = _asset.Tiles[i];
                if (entry != null && entry.Cell == cell && entry.Prefab != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定配置種別の行があるか。</summary>
        private bool HasPlacement(int kind)
        {
            for (var i = 0; i < _asset.Placements.Count; i++)
            {
                var entry = _asset.Placements[i];
                if (entry != null && entry.Kind == kind && entry.Prefab != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>未登録の定番役割ぶんだけ空行を足す（プレハブを入れるだけの状態にする）。</summary>
        private void AddTemplateRows()
        {
            Undo.RecordObject(_asset, "雛形を並べる");
            for (var i = 0; i < KnownCells.Length; i++)
            {
                if (!HasTileRow(KnownCells[i]))
                {
                    _asset.Tiles.Add(new StagePaletteAsset.TileEntry { Cell = KnownCells[i] });
                }
            }
            for (var i = 0; i < KnownPlacements.Length; i++)
            {
                if (!HasPlacementRow(KnownPlacements[i]))
                {
                    _asset.Placements.Add(
                        new StagePaletteAsset.PlacementEntry { Kind = KnownPlacements[i] });
                }
            }
            EditorUtility.SetDirty(_asset);
            Validate();
        }

        /// <summary>指定セル種別の行が（プレハブ有無を問わず）あるか。</summary>
        private bool HasTileRow(int cell)
        {
            for (var i = 0; i < _asset.Tiles.Count; i++)
            {
                if (_asset.Tiles[i] != null && _asset.Tiles[i].Cell == cell)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定配置種別の行が（プレハブ有無を問わず）あるか。</summary>
        private bool HasPlacementRow(int kind)
        {
            for (var i = 0; i < _asset.Placements.Count; i++)
            {
                if (_asset.Placements[i] != null && _asset.Placements[i].Kind == kind)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>検証結果を表示する。</summary>
        private void DrawProblems()
        {
            if (_problems.Count == 0)
            {
                return;
            }
            EditorGUILayout.HelpBox("不備 " + _problems.Count + " 件:\n・"
                + string.Join("\n・", _problems), MessageType.Warning);
        }

        /// <summary>地形タイルの紐付け一覧を描画する。</summary>
        private void DrawTiles()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"地形タイル（{_asset.Tiles.Count} 行）", EditorStyles.boldLabel);

            for (var i = 0; i < _asset.Tiles.Count; i++)
            {
                var entry = _asset.Tiles[i];
                if (entry == null)
                {
                    continue;
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var cell = IdPopup(entry.Cell, KnownCells, StageGenNames.Cell, 190f);
                        var variant = IntFieldWithLabel("バリアント", entry.Variant, 110f);
                        var biome = IntFieldWithLabel("バイオーム", entry.Biome, 110f);
                        GUILayout.FlexibleSpace();
                        var remove = GUILayout.Button("×", GUILayout.Width(24f));

                        if (cell != entry.Cell || variant != entry.Variant || biome != entry.Biome)
                        {
                            Undo.RecordObject(_asset, "タイル紐付けの変更");
                            entry.Cell = cell;
                            entry.Variant = Mathf.Max(0, variant);
                            entry.Biome = Mathf.Max(0, biome);
                            Dirty();
                        }
                        if (remove)
                        {
                            Undo.RecordObject(_asset, "タイル行の削除");
                            _asset.Tiles.RemoveAt(i);
                            Dirty();
                            return; // このフレームの描画は打ち切る（添字がずれるため）
                        }
                    }
                    var prefab = (GameObject)EditorGUILayout.ObjectField(
                        "プレハブ", entry.Prefab, typeof(GameObject), false);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var yOffset = EditorGUILayout.FloatField(
                            "高さ調整", entry.YOffset, GUILayout.Width(200f));
                        var scale = EditorGUILayout.ToggleLeft(
                            "セルの大きさに合わせる", entry.ScaleToCell, GUILayout.Width(200f));
                        if (prefab != entry.Prefab
                            || !Mathf.Approximately(yOffset, entry.YOffset)
                            || scale != entry.ScaleToCell)
                        {
                            Undo.RecordObject(_asset, "タイル紐付けの変更");
                            entry.Prefab = prefab;
                            entry.YOffset = yOffset;
                            entry.ScaleToCell = scale;
                            Dirty();
                        }
                    }
                }
            }

            if (GUILayout.Button("地形タイルの行を追加"))
            {
                Undo.RecordObject(_asset, "タイル行の追加");
                _asset.Tiles.Add(new StagePaletteAsset.TileEntry());
                Dirty();
            }
        }

        /// <summary>配置物の紐付け一覧を描画する。</summary>
        private void DrawPlacements()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                $"配置物（{_asset.Placements.Count} 行）", EditorStyles.boldLabel);

            for (var i = 0; i < _asset.Placements.Count; i++)
            {
                var entry = _asset.Placements[i];
                if (entry == null)
                {
                    continue;
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var kind = IdPopup(entry.Kind, KnownPlacements,
                            StageGenNames.Placement, 230f);
                        GUILayout.FlexibleSpace();
                        var remove = GUILayout.Button("×", GUILayout.Width(24f));
                        if (kind != entry.Kind)
                        {
                            Undo.RecordObject(_asset, "配置紐付けの変更");
                            entry.Kind = kind;
                            Dirty();
                        }
                        if (remove)
                        {
                            Undo.RecordObject(_asset, "配置行の削除");
                            _asset.Placements.RemoveAt(i);
                            Dirty();
                            return;
                        }
                    }
                    var prefab = (GameObject)EditorGUILayout.ObjectField(
                        "プレハブ", entry.Prefab, typeof(GameObject), false);
                    var yOffset = EditorGUILayout.FloatField(
                        "高さ調整", entry.YOffset, GUILayout.Width(200f));
                    if (prefab != entry.Prefab || !Mathf.Approximately(yOffset, entry.YOffset))
                    {
                        Undo.RecordObject(_asset, "配置紐付けの変更");
                        entry.Prefab = prefab;
                        entry.YOffset = yOffset;
                        Dirty();
                    }
                }
            }

            if (GUILayout.Button("配置物の行を追加"))
            {
                Undo.RecordObject(_asset, "配置行の追加");
                _asset.Placements.Add(new StagePaletteAsset.PlacementEntry());
                Dirty();
            }
        }

        /// <summary>
        /// ID の選択欄（既知の名前をドロップダウンで並べ、末尾に数値指定を置く）。
        /// アプリ独自の番号（100以降）も扱えるようにするための形。
        /// </summary>
        private static int IdPopup(int value, int[] known, Func<int, string> nameOf, float width)
        {
            var labels = new string[known.Length + 1];
            for (var i = 0; i < known.Length; i++)
            {
                labels[i] = nameOf(known[i]);
            }
            labels[known.Length] = "その他（数値で指定）";

            var index = Array.IndexOf(known, value);
            var isCustom = index < 0;
            var selected = EditorGUILayout.Popup(
                isCustom ? known.Length : index, labels, GUILayout.Width(width));

            if (selected < known.Length)
            {
                return known[selected];
            }
            // 数値指定: 既知の値から「その他」へ切り替えた直後はアプリ独自帯の先頭を出す
            var custom = isCustom ? value : 100;
            return Mathf.Max(1, EditorGUILayout.IntField(custom, GUILayout.Width(60f)));
        }

        /// <summary>ラベル付きの数値欄（横並び用に幅を指定できる）。</summary>
        private static int IntFieldWithLabel(string label, int value, float width)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(width - 46f));
                return EditorGUILayout.IntField(value, GUILayout.Width(40f));
            }
        }

        /// <summary>変更を記録して検証をかけ直す。</summary>
        private void Dirty()
        {
            EditorUtility.SetDirty(_asset);
            Validate();
        }
    }
}
