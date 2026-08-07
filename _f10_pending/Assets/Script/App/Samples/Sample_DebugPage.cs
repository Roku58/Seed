using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】開発用デバッグメニュー（UnityDebugSheet）。
    ///
    /// 実行中にポーズ・倍速・視点切替を GUI から叩けるようにする。すべて Hub への
    /// 命令発行なので、デバッグメニューは「本物の入口と同じ経路」を通る——
    /// デバッグ専用の裏口 API を作らないことで、メニューからの操作もゲームと同じ
    /// 検査（処理者不在の例外・トレーサの記録）を受ける。
    ///
    /// [起動] エディタでは Play 時に自動で立ち上がる（パッケージ内プレハブを読み込む）。
    /// 実機ビルドで使う場合は DebugSheetCanvas プレハブをシーンへ置く（ガイド18章）。
    /// </summary>
    public static class Sample_DebugPage
    {
        /// <summary>デバッグメニューを立ち上げてページを組む（エディタのみ・失敗しても無害）。</summary>
        public static void TryAttach(MessageHub hub, ServiceRegistry services)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.harumak.unitydebugsheet/Runtime/Core/Prefabs/DebugSheetCanvas.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("[DebugSheet] プレハブが見つからないため、デバッグメニューを省略");
                return;
            }
            var canvas = Object.Instantiate(prefab);
            canvas.name = "DebugSheetCanvas";
            Object.DontDestroyOnLoad(canvas);

            var sheet = UnityDebugSheet.Runtime.Core.Scripts.DebugSheet.Instance;
            if (sheet == null)
            {
                return;
            }
            var page = sheet.GetOrCreateInitialPage("Seed デバッグ");

            page.AddLabel("時間", subText: "処理者は GameClock（命令1発）");
            page.AddButton("ポーズ切替", clicked: () =>
            {
                var clock = services.Resolve<IGameClock>();
                hub.PublishCommand(new SetPausedCommand(!clock.IsPaused));
            });
            page.AddButton("倍速 0.25（スロー）", clicked: () =>
                hub.PublishCommand(new SetTimeScaleCommand(0.25f)));
            page.AddButton("倍速 1.0（等速）", clicked: () =>
                hub.PublishCommand(new SetTimeScaleCommand(1f)));
            page.AddButton("ヒットストップ 0.3秒", clicked: () =>
                hub.PublishCommand(new HitStopCommand(0.3f)));

            page.AddLabel("カメラ", subText: "処理者は CameraDirector（戦闘中のみ有効）");
            page.AddButton("三人称（TPS）", clicked: () =>
                hub.PublishCommand(new SetViewpointCommand(ViewpointId.ThirdPerson, 0.35f)));
            page.AddButton("一人称（FPS）", clicked: () =>
                hub.PublishCommand(new SetViewpointCommand(ViewpointId.FirstPerson, 0.35f)));
            page.AddButton("俯瞰", clicked: () =>
                hub.PublishCommand(new SetViewpointCommand(ViewpointId.Overhead, 0.35f)));
#endif
        }
    }
}
