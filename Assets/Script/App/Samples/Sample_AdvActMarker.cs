using Seed.Adv;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】タイムラインに載せる演技マーカー（1マーカー=1演技）。
    ///
    /// ブックのビルド（メニュー **Seed/Adv Timeline Build**）がマスターデータから
    /// 自動生成する。再生中にマーカーへ到達すると Sample_AdvDirectorReceiver へ通知され、
    /// フェーズの ExecuteAct（マスターデータ直接実行と同じ入口）が処理する。
    /// 生成後にタイムラインエディタでマーカーの時刻をずらしたり、
    /// 独自のトラック（音・アニメ等）を足して演出を厚くしてもよい
    /// （ただし再ビルドは全面上書き——加筆する場合は複製してから）。
    /// </summary>
    public sealed class Sample_AdvActMarker : Marker, INotification
    {
        /// <summary>対象キャラID（CameraSwitch では切替先カメラID）。</summary>
        public int ActorId;

        /// <summary>演技の種類（AdvActKind の値）。</summary>
        public int Kind;

        /// <summary>文字列パラメータ（Motion=ステート名 / Bubble=本文 / Signal=合図名 等）。</summary>
        public string Key = "";

        /// <summary>補助文字列（Model=コントローラキー）。</summary>
        public string SubKey = "";

        /// <summary>位置 X。</summary>
        public float X;

        /// <summary>位置 Y。</summary>
        public float Y;

        /// <summary>位置 Z。</summary>
        public float Z;

        /// <summary>秒数（補間時間 / 時間寿命）。</summary>
        public float Seconds;

        /// <summary>寿命（AdvActLife の値）。</summary>
        public int Life;

        /// <summary>通知の識別子（INotification の要求）。</summary>
        PropertyName INotification.id => new PropertyName("AdvAct");

        /// <summary>演技データへ戻す（実行系は AdvAct だけを知ればよい）。</summary>
        public AdvAct ToAct()
        {
            return new AdvAct(ActorId, (AdvActKind)Kind, Key, SubKey, X, Y, Z, Seconds,
                (AdvActLife)Life);
        }
    }
}
