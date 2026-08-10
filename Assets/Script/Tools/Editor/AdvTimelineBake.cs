using Seed.App;
using Seed.Hub.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Seed.Tools.Editor
{
    /// <summary>
    /// ADVのブック（1ページ = 1タイムライン）をマスターデータから自動生成するビルド工程。
    ///
    /// メニュー **Seed/Adv Timeline Build** を任意のタイミングで実行すると、
    /// 全イベントについてページごとの TimelineAsset（演技 = Sample_AdvActMarker）と、
    /// それらを束ねる Sample_AdvBookAsset（ブック）を `Assets/Resources/AdvBooks/` へ出力する。
    /// 実行時はブックの再生が基本経路になる（未ビルドのイベントはマスターデータ直接実行）。
    ///
    /// [注意] 再ビルドは全面上書き。生成物へ手で加筆した演出は消えるため、
    /// 加筆する場合は別アセットへ複製してから行うこと。
    /// </summary>
    public static class AdvTimelineBake
    {
        /// <summary>生成物の置き場（Resources 配下＝実行時にイベントIDで引ける）。</summary>
        private const string Folder = "Assets/Resources/AdvBooks";

        /// <summary>全イベントのブックをビルドする。</summary>
        [MenuItem("Seed/Adv Timeline Build")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "AdvBooks");
            }

            var catalog = Sample_MasterCatalog.Build(new CharacterId(1), new CharacterId(2));
            var events = catalog.GetAll<Sample_EventSpec>();
            var books = 0;
            var pageCount = 0;
            foreach (var spec in events)
            {
                var script = spec.BuildScript();
                var timelines = new TimelineAsset[script.Pages.Count];
                for (var p = 0; p < script.Pages.Count; p++)
                {
                    timelines[p] = BuildPageTimeline(spec.Id, p, script.Pages[p]);
                    pageCount++;
                }

                var book = ScriptableObject.CreateInstance<Sample_AdvBookAsset>();
                book.EventId = spec.Id;
                book.Pages = timelines;
                var bookPath = $"{Folder}/Event{spec.Id}.asset";
                AssetDatabase.DeleteAsset(bookPath);
                AssetDatabase.CreateAsset(book, bookPath);
                books++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Adv] ブックのビルド完了: {books} 冊 / {pageCount} ページ（{Folder}）。"
                + "以後このイベントはタイムライン再生で動きます（再ビルドは全面上書き）");
        }

        /// <summary>生成物を全て削除する（マスターデータ直接実行へ戻す）。</summary>
        [MenuItem("Seed/Adv Timeline Build 削除")]
        public static void DeleteBuilt()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                Debug.Log("[Adv] ブックの生成物を削除（マスターデータ直接実行へ戻る）");
            }
        }

        /// <summary>1ページぶんのタイムラインを生成する（演技→マーカー。時刻は全て0秒）。</summary>
        private static TimelineAsset BuildPageTimeline(int eventId, int pageIndex,
            Seed.Adv.AdvPage page)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = $"Event{eventId}_Page{pageIndex}";
            var path = $"{Folder}/Event{eventId}_Page{pageIndex}.playable";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(timeline, path);

            var track = timeline.markerTrack != null
                ? (TrackAsset)timeline.markerTrack
                : timeline.CreateTrack<MarkerTrack>(null, "Acts");
            for (var i = 0; i < page.Acts.Count; i++)
            {
                var act = page.Acts[i];
                // 時刻は全て0秒で生成する（データに時刻は無い）。タイムラインエディタで
                // ずらせば「ページ表示から◯秒後に演技」も作れる
                var marker = track.CreateMarker<Sample_AdvActMarker>(0);
                marker.ActorId = act.ActorId;
                marker.Kind = (int)act.Kind;
                marker.Key = act.Key;
                marker.SubKey = act.SubKey;
                marker.X = act.X;
                marker.Y = act.Y;
                marker.Z = act.Z;
                marker.Seconds = act.Seconds;
                marker.Life = (int)act.Life;
            }
            EditorUtility.SetDirty(timeline);
            return timeline;
        }
    }
}
