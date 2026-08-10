using UnityEngine;
using UnityEngine.Playables;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】タイムラインの演技マーカーを受け取ってフェーズへ渡す受信機。
    /// PlayableDirector と同じ GameObject に付ける（Timeline の通知は
    /// ディレクターの GameObject 上の INotificationReceiver へ届く仕様）。
    /// </summary>
    public sealed class Sample_AdvDirectorReceiver : MonoBehaviour, INotificationReceiver
    {
        /// <summary>マーカー受信時の届け先（フェーズの ExecuteAct）。</summary>
        public System.Action<Seed.Adv.AdvAct> ActReceived;

        /// <summary>通知の受信口（演技マーカーだけを拾う）。</summary>
        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is Sample_AdvActMarker marker)
            {
                ActReceived?.Invoke(marker.ToAct());
            }
        }
    }
}
