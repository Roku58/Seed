using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Seed.Data.Editor
{
    /// <summary>
    /// マスターデータの管理補助ウィンドウ（Seed/Master Data Browser）。
    ///
    /// プロジェクト内の全定義アセット（EntityDefinitionAsset）を型を問わず一覧し、
    /// - ID昇順の俯瞰（ID帯の運用が一目で分かる）
    /// - 検索（ID・名前・型名）
    /// - **ID重複・未設定の赤色警告**（Playする前に構成ミスが見える）
    /// - 空きIDの提案（次に何番を使えばよいか）
    /// - クリックでアセットを選択（そのままInspectorで編集）
    /// を提供する。データを増やす日常の動線を「作る→ここで確認→検証」に一本化する。
    /// </summary>
    public sealed class MasterDataBrowserWindow : EditorWindow
    {
        /// <summary>走査結果のキャッシュ。</summary>
        private List<MasterDataInspection.Entry> _entries = new List<MasterDataInspection.Entry>();

        /// <summary>重複IDの集合。</summary>
        private HashSet<int> _duplicates = new HashSet<int>();

        /// <summary>検索文字列。</summary>
        private string _search = "";

        /// <summary>一覧のスクロール位置。</summary>
        private Vector2 _scroll;

        /// <summary>ウィンドウを開く。</summary>
        [MenuItem("Seed/Master Data Browser")]
        public static void Open()
        {
            var window = GetWindow<MasterDataBrowserWindow>("Master Data");
            window.minSize = new Vector2(420f, 240f);
        }

        /// <summary>メニューから全定義を検証する（ウィンドウを開かずに使える）。</summary>
        [MenuItem("Seed/Master Data Validate")]
        public static void ValidateAll()
        {
            var entries = MasterDataInspection.CollectAll();
            var problems = MasterDataInspection.Validate(entries);
            if (problems.Count == 0)
            {
                Debug.Log($"[MasterData] 検証OK（定義 {entries.Count} 件・ID重複なし）");
                return;
            }
            for (var i = 0; i < problems.Count; i++)
            {
                Debug.LogError($"[MasterData] {problems[i]}");
            }
        }

        /// <summary>表示のたびに走査し直す（アセットの増減へ追従）。</summary>
        private void OnFocus()
        {
            Refresh();
        }

        /// <summary>走査結果を更新する。</summary>
        private void Refresh()
        {
            _entries = MasterDataInspection.CollectAll();
            _duplicates = MasterDataInspection.FindDuplicateIds(_entries);
        }

        /// <summary>ツールバー・一覧・提案を描画する。</summary>
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("再走査", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                {
                    Refresh();
                }
                if (GUILayout.Button("検証", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                {
                    ValidateAll();
                }
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                GUILayout.Label($"定義 {_entries.Count} 件", EditorStyles.miniLabel, GUILayout.Width(70f));
            }

            if (_duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"ID重複が {_duplicates.Count} 件あります（赤色の行）。IDはレコード・セーブに載るため全体で一意にすること。",
                    MessageType.Error);
            }
            EditorGUILayout.LabelField(
                $"次の空きID: {MasterDataInspection.SuggestNextId(_entries)}　" +
                $"（100以降: {MasterDataInspection.SuggestNextId(_entries, 100)} / 200以降: {MasterDataInspection.SuggestNextId(_entries, 200)}）",
                EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Asset == null)
                {
                    continue;
                }
                var typeName = entry.Asset.GetType().Name;
                var line = $"[{entry.Id}]  {entry.Asset.DebugName}    ({typeName})";
                if (!string.IsNullOrEmpty(_search)
                    && !line.Contains(_search) && !entry.Id.ToString().Contains(_search))
                {
                    continue;
                }

                var isBroken = entry.Id <= 0 || _duplicates.Contains(entry.Id);
                var previous = GUI.color;
                if (isBroken)
                {
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                }
                if (GUILayout.Button(line, EditorStyles.label))
                {
                    Selection.activeObject = entry.Asset; // クリックで選択→Inspectorで編集
                    EditorGUIUtility.PingObject(entry.Asset);
                }
                GUI.color = previous;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
