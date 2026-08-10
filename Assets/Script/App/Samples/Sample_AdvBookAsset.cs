using UnityEngine;
using UnityEngine.Timeline;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ADVのブック（1イベント = 複数ページのタイムラインの束）。
    ///
    /// メニュー **Seed/Adv Timeline Build** がマスターデータから自動生成する
    /// （1ページ = 1タイムライン。演技は Sample_AdvActMarker として載る）。
    /// 実行時はページ切替のたびに該当タイムラインを PlayableDirector で再生する——
    /// これが基本の再生経路で、ブックが無いイベントだけマスターデータの演技を
    /// 直接実行するフォールバックに落ちる。
    /// </summary>
    public sealed class Sample_AdvBookAsset : ScriptableObject
    {
        /// <summary>対応するイベントID。</summary>
        public int EventId;

        /// <summary>ページごとのタイムライン（添字 = ページ番号）。</summary>
        public TimelineAsset[] Pages;
    }
}
