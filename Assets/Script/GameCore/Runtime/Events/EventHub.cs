using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// 型別購読イベントハブ。
    ///
    /// [設計原則: 低GC / 発火順の決定性 / 再帰の暴走対策]
    /// - Fire は for ループのみ（LINQ・スナップショット配列を作らない）
    /// - 発火中の解除は「墓標(null)化 → 走査終了後に一括除去」の遅延方式
    /// - 発火中の登録は「その発火では呼ばれず」、走査終了後に優先度順で本登録される
    /// - 優先度昇順・同値は登録順の安定順序
    /// - 実行中ハンドラーへの再入をスキップ（振動ループ対策。トレースで観測可能）
    /// - UnsubscribeAll(owner) で退場アクターの個別仕様を一括解除（所有者インデックス）
    /// - 「登録＝そのゲームに存在する / 未登録＝オミット」が仕様の着脱の基本操作
    ///
    /// [設計原則: 対象ID付きスコープ購読（二段索引）]
    /// 「イベント型」だけで索引すると、1000体規模では全ユニットのハンドラーが
    /// 1回の発火ごとに線形走査され O(ユニット数×発火数) になる。
    /// そこで索引を「イベント型 → 対象ID → 購読者リスト」の二段にし、
    /// Fire(ev, ctx, targetId) は「全体購読リスト」＋「その対象IDの購読リスト」だけを走査する。
    /// - targetId=0 は従来どおりの全体購読（＝後方互換。0宛ての Fire は全体購読のみ呼ぶ）
    /// - マージ走査は優先度昇順。優先度が同値のときは「全体購読を先」に呼ぶ規約
    ///   （別リスト間では登録順が比較できないため、順序を決定的にするための取り決め）
    /// </summary>
    public sealed class EventHub
    {
        /// <summary>イベント型ごとの購読者リスト（EventHub内部の管理用）。</summary>
        private sealed class HandlerList
        {
            /// <summary>購読者の席（優先度順）。</summary>
            public readonly List<ILogicEventHandler> Items = new List<ILogicEventHandler>(8);
            /// <summary>このリストを走査中の発火の入れ子数。</summary>
            public int IterationDepth;
            /// <summary>走査中に墓標(null)が出たか。</summary>
            public bool HasTombstone;
            /// <summary>発火中に登録された購読者の待機列（その発火では呼ばれない）。</summary>
            public List<ILogicEventHandler> Pending;

            /// <summary>実質の購読者が居ない（索引から捨ててよい）か。</summary>
            public bool IsEmpty =>
                IterationDepth == 0 && Items.Count == 0 && (Pending == null || Pending.Count == 0);
        }

        /// <summary>
        /// 所有者インデックスの1件（イベント型・対象ID・ハンドラー）。
        /// UnsubscribeAll がスコープ購読も含めて正しく解除できるよう、対象IDまで覚えておく。
        /// </summary>
        internal readonly struct OwnedSubscription
        {
            /// <summary>購読しているイベント型。</summary>
            public readonly Type EventType;
            /// <summary>購読の対象ID（0=全体購読）。</summary>
            public readonly int TargetId;
            /// <summary>購読者。</summary>
            public readonly ILogicEventHandler Handler;

            /// <summary>OwnedSubscription を生成する。</summary>
            public OwnedSubscription(Type eventType, int targetId, ILogicEventHandler handler)
            {
                EventType = eventType;
                TargetId = targetId;
                Handler = handler;
            }
        }

        private static readonly Predicate<ILogicEventHandler> IsNull = h => h == null;

        /// <summary>イベント型→全体購読者リストの索引（対象ID=0）。</summary>
        private readonly Dictionary<Type, HandlerList> _byEvent = new Dictionary<Type, HandlerList>();

        /// <summary>イベント型→対象ID→購読者リストの二段索引（スコープ購読）。</summary>
        private readonly Dictionary<Type, Dictionary<int, HandlerList>> _byEventTarget =
            new Dictionary<Type, Dictionary<int, HandlerList>>();

        /// <summary>所有者→その所有者が持つ購読の索引（一括解除用）。</summary>
        private readonly Dictionary<object, List<OwnedSubscription>> _byOwner =
            new Dictionary<object, List<OwnedSubscription>>();

        // ---------------- 購読 ----------------

        /// <summary>購読登録（全体購読）。優先度昇順（同値は登録順）の位置へ安定挿入する。</summary>
        public void Subscribe<TEvent>(ILogicEventHandler<TEvent> handler) where TEvent : LogicEvent
        {
            SubscribeCore(typeof(TEvent), handler, 0);
        }

        /// <summary>
        /// 購読登録（対象ID付きスコープ購読）。
        /// targetId は EntityRegistry のIDなど「誰宛ての発火に反応するか」を表す。
        /// targetId=0 を渡すと従来どおりの全体購読になる（＝どの対象宛ての発火でも呼ばれる）。
        /// </summary>
        public void Subscribe<TEvent>(ILogicEventHandler<TEvent> handler, int targetId)
            where TEvent : LogicEvent
        {
            SubscribeCore(typeof(TEvent), handler, targetId);
        }

        /// <summary>購読登録の共通処理（全体購読・スコープ購読の差は索引の引き方だけ）。</summary>
        private void SubscribeCore(Type eventType, ILogicEventHandler handler, int targetId)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var list = GetOrCreateList(eventType, targetId);

            // [規約] 同一 (イベント型, 対象ID, ハンドラー) の二重購読は構成ミスとして弾く。
            // 二重に入ると同じ補正が2回かかる（＝原因の見えない数値バグ）ため、
            // 「静かに壊れる」より「登録時に落ちる」方を選ぶ。
            if (Contains(list, handler))
            {
                throw new LogicException(
                    $"同一ハンドラーの二重購読を検知: {eventType.Name} / targetId={targetId} / {handler.GetType().Name}");
            }

            if (list.IterationDepth > 0)
            {
                // [規約] 発火中に登録された購読者は「その発火では呼ばれない」。
                // 走査位置と優先度の関係で呼ばれたり呼ばれなかったりする曖昧さを避け、
                // 挙動を決定的にするための遅延登録。走査終了後に優先度順で本登録される。
                if (list.Pending == null)
                {
                    list.Pending = new List<ILogicEventHandler>(2);
                }
                list.Pending.Add(handler);
            }
            else
            {
                InsertSorted(list.Items, handler);
            }

            if (handler.Owner != null)
            {
                if (!_byOwner.TryGetValue(handler.Owner, out var owned))
                {
                    owned = new List<OwnedSubscription>(4);
                    _byOwner.Add(handler.Owner, owned);
                }
                owned.Add(new OwnedSubscription(eventType, targetId, handler));
            }
        }

        /// <summary>索引から購読者リストを引く（無ければ作る）。</summary>
        private HandlerList GetOrCreateList(Type eventType, int targetId)
        {
            if (eventType == null)
            {
                throw new ArgumentNullException(nameof(eventType));
            }

            if (targetId == 0)
            {
                if (!_byEvent.TryGetValue(eventType, out var globalList))
                {
                    globalList = new HandlerList();
                    _byEvent.Add(eventType, globalList);
                }
                return globalList;
            }

            if (!_byEventTarget.TryGetValue(eventType, out var perTarget))
            {
                perTarget = new Dictionary<int, HandlerList>();
                _byEventTarget.Add(eventType, perTarget);
            }
            if (!perTarget.TryGetValue(targetId, out var scopedList))
            {
                scopedList = new HandlerList();
                perTarget.Add(targetId, scopedList);
            }
            return scopedList;
        }

        /// <summary>索引から購読者リストを引く（無ければ null）。</summary>
        private HandlerList FindList(Type eventType, int targetId)
        {
            if (eventType == null)
            {
                return null;
            }
            if (targetId == 0)
            {
                _byEvent.TryGetValue(eventType, out var globalList);
                return globalList;
            }
            if (!_byEventTarget.TryGetValue(eventType, out var perTarget))
            {
                return null;
            }
            perTarget.TryGetValue(targetId, out var scopedList);
            return scopedList;
        }

        /// <summary>そのリストに同一ハンドラーが既に居るか（待機列も含む）。</summary>
        private static bool Contains(HandlerList list, ILogicEventHandler handler)
        {
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (ReferenceEquals(list.Items[i], handler))
                {
                    return true;
                }
            }
            if (list.Pending != null)
            {
                for (var i = 0; i < list.Pending.Count; i++)
                {
                    if (ReferenceEquals(list.Pending[i], handler))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>安定挿入: 優先度昇順・同値は登録順。</summary>
        private static void InsertSorted(List<ILogicEventHandler> items, ILogicEventHandler handler)
        {
            var index = items.Count;
            while (index > 0)
            {
                var prev = items[index - 1];
                if (prev == null || prev.Priority <= handler.Priority)
                {
                    break;
                }
                index--;
            }
            items.Insert(index, handler);
        }

        /// <summary>購読解除（全体購読）。イベント走査中なら墓標化し、走査終了後に取り除く。</summary>
        public void Unsubscribe(Type eventType, ILogicEventHandler handler)
        {
            UnsubscribeCore(eventType, handler, 0, cleanOwnerIndex: true);
        }

        /// <summary>購読解除（対象ID付きスコープ購読）。Subscribe と同じ targetId を渡すこと。</summary>
        public void Unsubscribe(Type eventType, ILogicEventHandler handler, int targetId)
        {
            UnsubscribeCore(eventType, handler, targetId, cleanOwnerIndex: true);
        }

        /// <summary>
        /// 購読解除の共通処理。
        /// cleanOwnerIndex=false は UnsubscribeAll からの呼び出し用
        /// （所有者リストを走査しながら同じリストを削るのを避けるため、呼び出し側が先に外す）。
        /// </summary>
        private void UnsubscribeCore(Type eventType, ILogicEventHandler handler, int targetId,
            bool cleanOwnerIndex)
        {
            var list = FindList(eventType, targetId);
            if (list == null || handler == null)
            {
                return;
            }

            // 待機列（発火中に登録され、まだ本登録前）にいる場合はそこから外すだけでよい
            if (list.Pending != null && list.Pending.Remove(handler))
            {
                if (cleanOwnerIndex)
                {
                    RemoveOwned(eventType, handler, targetId);
                }
                DropIfEmpty(eventType, targetId, list);
                return;
            }

            var index = list.Items.IndexOf(handler);
            if (index < 0)
            {
                return;
            }

            if (list.IterationDepth > 0)
            {
                list.Items[index] = null; // 遅延削除
                list.HasTombstone = true;
            }
            else
            {
                list.Items.RemoveAt(index);
            }

            // [リーク修正] 所有者インデックスからも必ず外す。
            // 掃除しないと「消費で個別解除するハンドラー（きのみ等）」の着脱を繰り返す所有者の
            // owned リストが単調増加し、UnsubscribeAll のコストと保持参照が無限に伸びる。
            if (cleanOwnerIndex)
            {
                RemoveOwned(eventType, handler, targetId);
            }
            DropIfEmpty(eventType, targetId, list);
        }

        /// <summary>所有者インデックスから該当1件を除去し、空になった所有者エントリごと消す。</summary>
        private void RemoveOwned(Type eventType, ILogicEventHandler handler, int targetId)
        {
            var owner = handler.Owner;
            if (owner == null || !_byOwner.TryGetValue(owner, out var owned))
            {
                return;
            }

            for (var i = 0; i < owned.Count; i++)
            {
                var entry = owned[i];
                if (entry.TargetId == targetId && entry.EventType == eventType
                    && ReferenceEquals(entry.Handler, handler))
                {
                    owned.RemoveAt(i); // 二重購読は Subscribe で弾いているので1件だけ
                    break;
                }
            }

            if (owned.Count == 0)
            {
                _byOwner.Remove(owner);
            }
        }

        /// <summary>
        /// 空になったスコープ購読リストを二段索引から捨てる（長期セッションでの索引の肥大化防止）。
        /// 全体購読リストは型の数しかないので残したままでよい（毎回作り直す方が無駄）。
        /// </summary>
        private void DropIfEmpty(Type eventType, int targetId, HandlerList list)
        {
            if (targetId == 0 || !list.IsEmpty)
            {
                return;
            }
            if (!_byEventTarget.TryGetValue(eventType, out var perTarget))
            {
                return;
            }
            perTarget.Remove(targetId);
            if (perTarget.Count == 0)
            {
                _byEventTarget.Remove(eventType);
            }
        }

        /// <summary>所有者の全購読を一括解除する（戦闘不能・退場・持ち替え等）。</summary>
        public void UnsubscribeAll(object owner)
        {
            if (owner == null || !_byOwner.TryGetValue(owner, out var owned))
            {
                return;
            }

            // 走査中に同じリストを削らないよう、先に所有者エントリを外してから解除する
            _byOwner.Remove(owner);
            for (var i = 0; i < owned.Count; i++)
            {
                var entry = owned[i];
                UnsubscribeCore(entry.EventType, entry.Handler, entry.TargetId, cleanOwnerIndex: false);
            }
        }

        // ---------------- 発火 ----------------

        /// <summary>
        /// イベント発火。購読者を優先度順に呼ぶ。ネスト発火（連鎖）にも安全。
        ///
        /// targetId=0（既定）は全体購読のみを呼ぶ（後方互換）。
        /// targetId!=0 のときは「全体購読」＋「その対象IDのスコープ購読」を
        /// 優先度昇順でマージ走査する（同値は全体購読を先）。
        /// </summary>
        public void Fire<TEvent>(TEvent ev, LogicContext ctx, int targetId = 0) where TEvent : LogicEvent
        {
            if (ev == null)
            {
                throw new ArgumentNullException(nameof(ev));
            }

            // 発火のたびに必ず上書きする（プールから来た古い宛先が残らない＝Reset の責務にしない理由）
            ev.TargetId = targetId;

            var eventType = typeof(TEvent);
            _byEvent.TryGetValue(eventType, out var global);
            var scoped = targetId == 0 ? null : FindList(eventType, targetId);

            var globalCount = global == null ? 0 : global.Items.Count;
            var scopedCount = scoped == null ? 0 : scoped.Items.Count;
            if (globalCount == 0 && scopedCount == 0)
            {
                return;
            }

            ctx.CountEventFired();
            ctx.TraceListener?.OnEventFired(eventType, globalCount + scopedCount);

            if (global != null)
            {
                global.IterationDepth++;
            }
            if (scoped != null)
            {
                scoped.IterationDepth++;
            }

            try
            {
                if (scopedCount == 0)
                {
                    // 単一リスト（従来経路）
                    for (var i = 0; i < global.Items.Count; i++)
                    {
                        Dispatch(global.Items[i], ev, ctx);
                    }
                }
                else if (globalCount == 0)
                {
                    for (var i = 0; i < scoped.Items.Count; i++)
                    {
                        Dispatch(scoped.Items[i], ev, ctx);
                    }
                }
                else
                {
                    FireMerged(global, scoped, ev, ctx);
                }
            }
            finally
            {
                EndIteration(global);
                EndIteration(scoped);
                if (scoped != null)
                {
                    DropIfEmpty(eventType, targetId, scoped);
                }
            }
        }

        /// <summary>
        /// 全体購読リストとスコープ購読リストを優先度昇順でマージ走査する。
        /// どちらのリストも走査中は要素が増えない（発火中の登録は待機列へ）ため、
        /// 添字は安定で、除去は墓標(null)としてスキップできる。
        /// </summary>
        private static void FireMerged<TEvent>(HandlerList global, HandlerList scoped, TEvent ev,
            LogicContext ctx) where TEvent : LogicEvent
        {
            var i = 0;
            var j = 0;
            while (true)
            {
                while (i < global.Items.Count && global.Items[i] == null)
                {
                    i++; // 墓標
                }
                while (j < scoped.Items.Count && scoped.Items[j] == null)
                {
                    j++;
                }

                var hasGlobal = i < global.Items.Count;
                var hasScoped = j < scoped.Items.Count;
                if (!hasGlobal && !hasScoped)
                {
                    return;
                }

                ILogicEventHandler handler;
                if (!hasScoped
                    || (hasGlobal && global.Items[i].Priority <= scoped.Items[j].Priority))
                {
                    handler = global.Items[i]; // 優先度同値は全体購読を先（順序を決定的にする規約）
                    i++;
                }
                else
                {
                    handler = scoped.Items[j];
                    j++;
                }

                Dispatch(handler, ev, ctx);
            }
        }

        /// <summary>1件のハンドラーを呼ぶ（墓標スキップ・再入ガード・トレース込み）。</summary>
        private static void Dispatch<TEvent>(ILogicEventHandler handler, TEvent ev, LogicContext ctx)
            where TEvent : LogicEvent
        {
            if (handler == null)
            {
                return; // 墓標
            }

            if (!handler.EnterExecution()) // 再入禁止
            {
                ctx.TraceListener?.OnHandlerReentrySkipped(handler, typeof(TEvent));
                return;
            }

            try
            {
                ((ILogicEventHandler<TEvent>)handler).Handle(ev, ctx);
            }
            finally
            {
                handler.ExitExecution();
            }
        }

        /// <summary>走査を1段抜ける。最外の走査が終わった時点で墓標除去と待機列の本登録を行う。</summary>
        private static void EndIteration(HandlerList list)
        {
            if (list == null)
            {
                return;
            }

            list.IterationDepth--;
            if (list.IterationDepth != 0)
            {
                return;
            }

            if (list.HasTombstone)
            {
                list.Items.RemoveAll(IsNull);
                list.HasTombstone = false;
            }
            if (list.Pending != null && list.Pending.Count > 0)
            {
                // 発火中に登録された購読者を、走査が完全に終わってから本登録する
                for (var i = 0; i < list.Pending.Count; i++)
                {
                    InsertSorted(list.Items, list.Pending[i]);
                }
                list.Pending.Clear();
            }
        }

        // ---------------- 購読スナップショット（巻き戻し・先読みAI） ----------------

        /// <summary>
        /// 現在の購読状態を複製する（★巻き戻しの完全化）。
        /// CoreSnapshot＋アクター状態だけでは「きのみ消費＝解除」「まもる＝登録」が戻らないため、
        /// 巻き戻し区間で購読変更が起きうる場合はこれも保存する。発火中は取得不可。
        /// スコープ購読・所有者インデックスも含めて丸ごと保存する。
        /// </summary>
        public SubscriptionSnapshot CaptureSnapshot()
        {
            EnsureNotIterating();

            var snapshot = new SubscriptionSnapshot();
            foreach (var pair in _byEvent)
            {
                var list = CopyLive(pair.Value);
                if (list != null)
                {
                    snapshot.Subscriptions.Add(pair.Key, list);
                }
            }
            foreach (var pair in _byEventTarget)
            {
                Dictionary<int, List<ILogicEventHandler>> perTarget = null;
                foreach (var scopedPair in pair.Value)
                {
                    var list = CopyLive(scopedPair.Value);
                    if (list == null)
                    {
                        continue;
                    }
                    if (perTarget == null)
                    {
                        perTarget = new Dictionary<int, List<ILogicEventHandler>>();
                    }
                    perTarget.Add(scopedPair.Key, list);
                }
                if (perTarget != null)
                {
                    snapshot.ScopedSubscriptions.Add(pair.Key, perTarget);
                }
            }
            foreach (var pair in _byOwner)
            {
                snapshot.Owners.Add(pair.Key, new List<OwnedSubscription>(pair.Value));
            }
            return snapshot;
        }

        /// <summary>生きている購読者（墓標を除き待機列を含む）を複製する。1件も無ければ null。</summary>
        private static List<ILogicEventHandler> CopyLive(HandlerList source)
        {
            var list = new List<ILogicEventHandler>(source.Items.Count);
            for (var i = 0; i < source.Items.Count; i++)
            {
                if (source.Items[i] != null)
                {
                    list.Add(source.Items[i]);
                }
            }
            if (source.Pending != null)
            {
                for (var i = 0; i < source.Pending.Count; i++)
                {
                    list.Add(source.Pending[i]);
                }
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>購読状態を復元する。CaptureSnapshot とペアで使う。発火中は不可。</summary>
        public void RestoreSnapshot(SubscriptionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            EnsureNotIterating();

            _byEvent.Clear();
            _byEventTarget.Clear();
            _byOwner.Clear();
            foreach (var pair in snapshot.Subscriptions)
            {
                var list = new HandlerList();
                list.Items.AddRange(pair.Value); // Capture時点の優先度順を保持
                _byEvent.Add(pair.Key, list);
            }
            foreach (var pair in snapshot.ScopedSubscriptions)
            {
                var perTarget = new Dictionary<int, HandlerList>(pair.Value.Count);
                foreach (var scopedPair in pair.Value)
                {
                    var list = new HandlerList();
                    list.Items.AddRange(scopedPair.Value);
                    perTarget.Add(scopedPair.Key, list);
                }
                _byEventTarget.Add(pair.Key, perTarget);
            }
            foreach (var pair in snapshot.Owners)
            {
                _byOwner.Add(pair.Key, new List<OwnedSubscription>(pair.Value));
            }
        }

        /// <summary>イベント発火中でないことを検証する。</summary>
        private void EnsureNotIterating()
        {
            foreach (var list in _byEvent.Values)
            {
                if (list.IterationDepth > 0)
                {
                    throw new LogicException("イベント発火中に購読スナップショットは操作できない");
                }
            }
            foreach (var perTarget in _byEventTarget.Values)
            {
                foreach (var list in perTarget.Values)
                {
                    if (list.IterationDepth > 0)
                    {
                        throw new LogicException("イベント発火中に購読スナップショットは操作できない");
                    }
                }
            }
        }

        // ---------------- 診断 ----------------

        /// <summary>テスト・デバッグ用: 指定イベント型の全体購読者数（墓標を含まず、待機列を含む）。</summary>
        public int SubscriberCount(Type eventType)
        {
            return SubscriberCount(eventType, 0);
        }

        /// <summary>
        /// テスト・デバッグ用: 指定イベント型・対象IDの購読者数（墓標を含まず、待機列を含む）。
        /// targetId=0 は全体購読の数（従来の SubscriberCount(Type) と同義）。
        /// </summary>
        public int SubscriberCount(Type eventType, int targetId)
        {
            var list = FindList(eventType, targetId);
            if (list == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (list.Items[i] != null)
                {
                    count++;
                }
            }
            if (list.Pending != null)
            {
                count += list.Pending.Count;
            }
            return count;
        }

        /// <summary>テスト・デバッグ用: 所有者インデックスに残っている購読件数（リーク検知用）。</summary>
        public int OwnedSubscriptionCount(object owner)
        {
            if (owner == null || !_byOwner.TryGetValue(owner, out var owned))
            {
                return 0;
            }
            return owned.Count;
        }
    }
}
