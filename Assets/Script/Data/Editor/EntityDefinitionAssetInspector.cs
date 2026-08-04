using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Seed.Data.Editor
{
    /// <summary>
    /// 定義アセットの入力補助（EntityDefinitionAsset 派生すべてに効く共通Inspector）。
    ///
    /// 通常のフィールド編集に加えて:
    /// - **その場でID重複・未設定を警告**（保存してPlayする前に気づける）
    /// - 「空きIDを割り当て」ボタン（次に使える番号を探す手間を無くす）
    /// ゲームごとの派生（武器・ステージ…）が独自フィールドを足しても、そのまま効く。
    /// </summary>
    [CustomEditor(typeof(EntityDefinitionAsset), editorForChildClasses: true)]
    public sealed class EntityDefinitionAssetInspector : UnityEditor.Editor
    {
        /// <summary>走査キャッシュ（Inspector表示中だけ保持）。</summary>
        private List<MasterDataInspection.Entry> _entries;

        /// <summary>選択が変わったら走査し直す。</summary>
        private void OnEnable()
        {
            _entries = MasterDataInspection.CollectAll();
        }

        /// <summary>既定の編集UI＋検証警告＋入力補助を描画する。</summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var definition = (EntityDefinitionAsset)target;
            var idProperty = serializedObject.FindProperty("_id");
            if (idProperty == null)
            {
                return;
            }

            if (definition.Id <= 0)
            {
                EditorGUILayout.HelpBox(
                    "ID が未設定です（0 は「対象なし」の予約値）。1以上を割り当ててください。",
                    MessageType.Error);
            }
            else if (CountUsers(definition.Id) > 1)
            {
                EditorGUILayout.HelpBox(
                    $"ID {definition.Id} は他の定義と重複しています（IDはレコード・セーブに載るため全体で一意）。",
                    MessageType.Error);
            }

            if (GUILayout.Button($"空きIDを割り当て（候補: {MasterDataInspection.SuggestNextId(_entries)}）"))
            {
                serializedObject.Update();
                idProperty.intValue = MasterDataInspection.SuggestNextId(_entries);
                serializedObject.ApplyModifiedProperties();
                _entries = MasterDataInspection.CollectAll();
            }
        }

        /// <summary>該当IDを使っている定義の数。</summary>
        private int CountUsers(int id)
        {
            var count = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    count++;
                }
            }
            // 自分のIDを編集した直後はキャッシュが古い可能性があるため、最低1は自分
            return count < 1 ? 1 : count;
        }
    }
}
