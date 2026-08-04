using System;
using System.Collections.Generic;
using UnityEditor;

namespace Seed.Data.Editor
{
    /// <summary>
    /// プロジェクト内の定義アセット（EntityDefinitionAsset）の走査・検証の共通ロジック。
    /// ブラウザウィンドウと Inspector 入力補助の両方がここを使う。
    ///
    /// 検証はランタイム（MasterDataSet.ValidateGlobalIdUniqueness）と同じ規約:
    /// - ID は 1 以上（0 は EntityRegistry.None の予約値）
    /// - ID は型をまたいで全体で一意（レコード・セーブに載る値だから）
    /// エディタ段階で同じ違反を検出し、「Play して初めて例外で気づく」を無くす。
    /// </summary>
    public static class MasterDataInspection
    {
        /// <summary>定義アセット1件の走査結果。</summary>
        public readonly struct Entry
        {
            /// <summary>アセット本体。</summary>
            public readonly EntityDefinitionAsset Asset;

            /// <summary>ID（Asset.Id の写し。ソート用）。</summary>
            public readonly int Id;

            /// <summary>Entry を生成する。</summary>
            public Entry(EntityDefinitionAsset asset)
            {
                Asset = asset;
                Id = asset.Id;
            }
        }

        /// <summary>プロジェクト内の全定義アセットをID昇順で集める。</summary>
        public static List<Entry> CollectAll()
        {
            var entries = new List<Entry>();
            var guids = AssetDatabase.FindAssets("t:" + nameof(EntityDefinitionAsset));
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<EntityDefinitionAsset>(path);
                if (asset != null)
                {
                    entries.Add(new Entry(asset));
                }
            }
            entries.Sort((a, b) => a.Id != b.Id
                ? a.Id.CompareTo(b.Id)
                : string.CompareOrdinal(a.Asset.name, b.Asset.name));
            return entries;
        }

        /// <summary>重複しているIDの集合を返す（型をまたいだ全体一意の検査）。</summary>
        public static HashSet<int> FindDuplicateIds(List<Entry> entries)
        {
            var seen = new HashSet<int>();
            var duplicates = new HashSet<int>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (!seen.Add(entries[i].Id))
                {
                    duplicates.Add(entries[i].Id);
                }
            }
            return duplicates;
        }

        /// <summary>違反メッセージの一覧を返す（空なら合格）。</summary>
        public static List<string> Validate(List<Entry> entries)
        {
            var problems = new List<string>();
            var duplicates = FindDuplicateIds(entries);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Id <= 0)
                {
                    problems.Add($"{entry.Asset.name}: ID が未設定（{entry.Id}）。1以上を割り当てること");
                }
                else if (duplicates.Contains(entry.Id))
                {
                    problems.Add($"{entry.Asset.name}: ID {entry.Id} が他の定義と重複");
                }
            }
            return problems;
        }

        /// <summary>指定値以上で最初の空きIDを返す（入力補助の提案用）。</summary>
        public static int SuggestNextId(List<Entry> entries, int from = 1)
        {
            var used = new HashSet<int>();
            for (var i = 0; i < entries.Count; i++)
            {
                used.Add(entries[i].Id);
            }
            var candidate = Math.Max(1, from);
            while (used.Contains(candidate))
            {
                candidate++;
            }
            return candidate;
        }
    }
}
