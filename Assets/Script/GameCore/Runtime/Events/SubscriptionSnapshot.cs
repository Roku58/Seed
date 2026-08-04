using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// EventHub の購読状態のスナップショット（巻き戻し・先読みAI用）。
    ///
    /// CoreSnapshot（時刻・乱数）とアクター状態のコピーだけでは、
    /// 「きのみ消費＝購読解除」「まもる使用＝購読登録」のような
    /// 巻き戻し区間中の購読変更が元に戻らない。本スナップショットで購読も復元する。
    ///
    /// 取得・復元は EventHub.CaptureSnapshot / RestoreSnapshot（発火中は不可）。
    /// リストを複製するためアロケーションを伴う＝毎フレームではなく節目で使うこと。
    ///
    /// 中身は EventHub の実装詳細（internal）。ゲーム側は「不透明なトークン」として
    /// 持ち回すだけでよい（LogicContext.CaptureAll/RestoreAll 経由が推奨）。
    /// </summary>
    public sealed class SubscriptionSnapshot
    {
        /// <summary>イベント型→全体購読者（対象ID=0）。</summary>
        internal readonly Dictionary<Type, List<ILogicEventHandler>> Subscriptions =
            new Dictionary<Type, List<ILogicEventHandler>>();

        /// <summary>イベント型→対象ID→スコープ購読者（二段索引ぶん）。</summary>
        internal readonly Dictionary<Type, Dictionary<int, List<ILogicEventHandler>>> ScopedSubscriptions =
            new Dictionary<Type, Dictionary<int, List<ILogicEventHandler>>>();

        /// <summary>所有者→その所有者の購読一覧（一括解除用インデックスの複製）。</summary>
        internal readonly Dictionary<object, List<EventHub.OwnedSubscription>> Owners =
            new Dictionary<object, List<EventHub.OwnedSubscription>>();

        /// <summary>SubscriptionSnapshot を生成する。</summary>
        internal SubscriptionSnapshot()
        {
        }
    }
}
