using System;
using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.Hub
{
    /// <summary>
    /// 基盤同士をつなぐアプリ全体のメッセージハブ（フレームの世界の掲示板）。
    ///
    /// GameCore の EventHub と設計語彙（優先度・安定順序・発行中の解除/登録の遅延処理）は
    /// 共有するが、役割はまったく別物:
    /// - EventHub … 決定的ロジックの内側。リプレイの記録対象
    /// - MessageHub … Unityメインループ側。UI・キャラ・演出の連携用で非決定でよい
    /// 参照を共有すると GameCore が全基盤の依存先になるため、実装は意図的に分けている。
    ///
    /// 規約（コメントだけでなくコードで守らせる方針に改めた）:
    /// - メッセージは readonly struct（Seed.Hub.Contracts に定義した「境界を越える型」のみ）
    /// - 通知（過去形・<see cref="INotificationMessage"/>）は複数購読OK
    /// - 命令（〜Command・<see cref="ICommandMessage"/>）は処理者1基盤。
    ///   → <see cref="SubscribeCommand{TMessage}"/> / <see cref="PublishCommand{TMessage}"/> を使うと
    ///     二重購読・処理者不在がその場で例外になる。通常の Subscribe/Publish に命令型を渡しても例外
    /// - 1購読者の例外は他の購読者への配達を止めない（配達後に集約して投げ直す）
    /// - 毎フレームの連続値は流さない（ServiceRegistry のインターフェースで読む）
    /// - 同期発行だけでは連鎖が深くなりすぎるため、<see cref="PublishDeferred"/> と
    ///   <see cref="Pump"/>（フレーム末尾で合成ルートが呼ぶ）で「連鎖の外」へ逃がせる
    ///
    /// スレッド安全性: メインスレッド専用（ロックを持たない）。別スレッドから触ると
    /// 購読リストが静かに壊れるため、開発ビルドでは発行・購読時にスレッドを検査する。
    /// </summary>
    public sealed class MessageHub
    {
        /// <summary>発行の入れ子深さの上限（超えたら循環発行として早期に失敗させる）。</summary>
        public const int MaxPublishDepth = 32;

        /// <summary>購読1件の内部表現。</summary>
        private sealed class Entry
        {
            /// <summary>適用順（小さいほど先・同値は登録順）。</summary>
            public int Priority;

            /// <summary>購読解除済みか（発行中は墓標にして後で除去）。</summary>
            public bool Removed;

            /// <summary>呼び出す本体。</summary>
            public Delegate Handler;
        }

        /// <summary>メッセージ型ごとの購読者リスト。</summary>
        private sealed class HandlerList
        {
            /// <summary>購読者（優先度順）。</summary>
            public readonly List<Entry> Items = new List<Entry>(8);

            /// <summary>発行の入れ子深さ（0のとき墓標除去と待機反映を行う）。</summary>
            public int PublishDepth;

            /// <summary>発行中に追加された購読の待機列。</summary>
            public List<Entry> Pending;

            /// <summary>墓標が発生したか。</summary>
            public bool HasTombstone;
        }

        /// <summary>購読解除トークン。Dispose で解除できる。</summary>
        private sealed class Subscription : IDisposable
        {
            /// <summary>解除対象のリスト。</summary>
            private HandlerList _list;

            /// <summary>解除対象のエントリ。</summary>
            private Entry _entry;

            /// <summary>Subscription を生成する。</summary>
            public Subscription(HandlerList list, Entry entry)
            {
                _list = list;
                _entry = entry;
            }

            /// <summary>購読を解除する（発行中なら墓標化、二重Disposeは無害）。</summary>
            public void Dispose()
            {
                if (_entry == null)
                {
                    return;
                }
                _entry.Removed = true;
                if (_list.PublishDepth > 0)
                {
                    _list.HasTombstone = true;
                }
                else
                {
                    _list.Items.Remove(_entry);
                    _list.Pending?.Remove(_entry);
                }
                _entry = null;
                _list = null;
            }
        }

        /// <summary>遅延発行1件（型を保ったまま配達を予約する）。</summary>
        private readonly struct DeferredItem
        {
            /// <summary>配達を行う処理（発行時にキャプチャ済み）。</summary>
            public readonly Action Deliver;

            /// <summary>メッセージ型（トレース用）。</summary>
            public readonly Type MessageType;

            /// <summary>DeferredItem を生成する。</summary>
            public DeferredItem(Type messageType, Action deliver)
            {
                MessageType = messageType;
                Deliver = deliver;
            }
        }

        /// <summary>型→購読者リストの索引。</summary>
        private readonly Dictionary<Type, HandlerList> _byMessage = new Dictionary<Type, HandlerList>();

        /// <summary>遅延発行の待機列（Pump で処理）。</summary>
        private readonly List<DeferredItem> _deferred = new List<DeferredItem>(8);

        /// <summary>Pump 中の入れ替え用バッファ（Pump 中の再遅延発行を次回へ回す）。</summary>
        private readonly List<DeferredItem> _deferredSwap = new List<DeferredItem>(8);

        /// <summary>配達中に発生した例外の集約先（配達を止めないため）。</summary>
        private readonly List<Exception> _deliveryErrors = new List<Exception>(2);

        /// <summary>生成したスレッド（メインスレッド専用の検査用）。</summary>
        private readonly int _ownerThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>現在の発行の入れ子深さ（全型合計）。</summary>
        private int _globalPublishDepth;

        /// <summary>MessageHub を生成する（開発ビルドでは観測用の台帳へ載せる）。</summary>
        public MessageHub()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MessageHubRegistry.Register(this);
#endif
        }

        /// <summary>Pump 実行中か。</summary>
        private bool _isPumping;

        /// <summary>
        /// メッセージが発行された（開発時の観測窓口）。
        /// 「基盤同士が互いを知らない」設計では、不具合調査の手掛かりがメッセージフローしかない。
        /// null なら一切コストをかけないため、開発ビルドでのみ購読する運用でよい。
        /// 引数は（メッセージ型・購読者数）。メッセージ本体を渡さないのはボックス化を避けるため。
        /// </summary>
        public event Action<Type, int> MessagePublished;

        /// <summary>
        /// 購読者が例外を投げた（開発時の観測窓口）。
        /// 購読していれば通知のみで飲み込み、購読が無ければ配達完了後に集約例外として投げ直す
        /// （1基盤のバグで無関係な基盤やフレーム全体を止めないため）。
        /// </summary>
        public event Action<Type, Exception> DeliveryFailed;

        /// <summary>
        /// 購読を登録する。戻り値の IDisposable を保持し、不要になったら Dispose すること
        /// （まとめて解除するなら <see cref="SubscriptionBag"/> を使う）。
        /// 命令型（<see cref="ICommandMessage"/>）は <see cref="SubscribeCommand{TMessage}"/> を使うこと。
        /// </summary>
        public IDisposable Subscribe<TMessage>(Action<TMessage> handler, int priority = 0)
            where TMessage : struct
        {
            if (typeof(ICommandMessage).IsAssignableFrom(typeof(TMessage)))
            {
                throw new HubException(
                    $"{typeof(TMessage).Name} は命令（ICommandMessage）。SubscribeCommand を使う（処理者1基盤を守るため）");
            }
            return SubscribeInternal(handler, priority);
        }

        /// <summary>
        /// 命令の処理者として登録する。既に処理者がいれば「命令は処理者1基盤」規約違反として例外。
        /// 規約をコメントではなくコードで守らせるための入口。
        /// </summary>
        public IDisposable SubscribeCommand<TMessage>(Action<TMessage> handler)
            where TMessage : struct, ICommandMessage
        {
            if (SubscriberCount(typeof(TMessage)) > 0)
            {
                throw new HubException(
                    $"{typeof(TMessage).Name} には既に処理者がいる（命令の処理者は1基盤。二重処理を防ぐ）");
            }
            return SubscribeInternal(handler, 0);
        }

        /// <summary>メッセージを発行し、購読者を優先度順に呼ぶ。入れ子発行にも安全。</summary>
        public void Publish<TMessage>(in TMessage message) where TMessage : struct
        {
            if (typeof(ICommandMessage).IsAssignableFrom(typeof(TMessage)))
            {
                throw new HubException(
                    $"{typeof(TMessage).Name} は命令（ICommandMessage）。PublishCommand を使う（処理者不在を検知するため）");
            }
            PublishInternal(in message);
        }

        /// <summary>
        /// 命令を発行する。処理者がいなければ「配線漏れ」として例外
        /// （通知は購読者0人でも成立するが、命令は誰かが処理しなければ意味がない）。
        /// </summary>
        public void PublishCommand<TMessage>(in TMessage message) where TMessage : struct, ICommandMessage
        {
            if (SubscriberCount(typeof(TMessage)) == 0)
            {
                throw new HubException(
                    $"{typeof(TMessage).Name} の処理者が未登録（命令が握り潰される。合成ルートを確認）");
            }
            PublishInternal(in message);
        }

        /// <summary>
        /// 発行を「今の連鎖の外」へ回す（フレーム末尾の <see cref="Pump"/> で配達される）。
        /// 同期発行だけだと連鎖長ぶんコールスタックが伸び、循環すれば即死する。
        /// 「次フレーム/連鎖の外で処理したい」を各アプリが自前キューで再発明しないための標準手段。
        /// </summary>
        public void PublishDeferred<TMessage>(in TMessage message) where TMessage : struct
        {
            EnsureMainThread();
            var captured = message; // struct をキャプチャして型を保ったまま配達を予約する
            var isCommand = typeof(ICommandMessage).IsAssignableFrom(typeof(TMessage));
            _deferred.Add(new DeferredItem(typeof(TMessage), () =>
            {
                if (isCommand)
                {
                    if (SubscriberCount(typeof(TMessage)) == 0)
                    {
                        throw new HubException(
                            $"{typeof(TMessage).Name} の処理者が未登録（命令が握り潰される。合成ルートを確認）");
                    }
                }
                PublishInternal(in captured);
            }));
        }

        /// <summary>
        /// 遅延発行を配達する（合成ルートがフレーム末尾で1回呼ぶ）。配達した件数を返す。
        /// Pump 中に積まれた遅延発行は次回の Pump へ回す（無限ループを避けるため）。
        /// </summary>
        public int Pump()
        {
            EnsureMainThread();
            if (_isPumping || _deferred.Count == 0)
            {
                return 0;
            }

            _isPumping = true;
            try
            {
                _deferredSwap.Clear();
                _deferredSwap.AddRange(_deferred);
                _deferred.Clear();

                for (var i = 0; i < _deferredSwap.Count; i++)
                {
                    _deferredSwap[i].Deliver();
                }
                return _deferredSwap.Count;
            }
            finally
            {
                _deferredSwap.Clear();
                _isPumping = false;
            }
        }

        /// <summary>待機中の遅延発行の件数（テスト・デバッグ用）。</summary>
        public int DeferredCount => _deferred.Count;

        /// <summary>テスト・デバッグ用: 指定メッセージ型の購読者数（墓標を除き待機を含む）。</summary>
        public int SubscriberCount(Type messageType)
        {
            if (!_byMessage.TryGetValue(messageType, out var list))
            {
                return 0;
            }
            var count = 0;
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (!list.Items[i].Removed)
                {
                    count++;
                }
            }
            if (list.Pending != null)
            {
                for (var i = 0; i < list.Pending.Count; i++)
                {
                    if (!list.Pending[i].Removed)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>購読を登録する内部実装（通知・命令の判定を済ませた後段）。</summary>
        private IDisposable SubscribeInternal<TMessage>(Action<TMessage> handler, int priority)
            where TMessage : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            EnsureMainThread();

            if (!_byMessage.TryGetValue(typeof(TMessage), out var list))
            {
                list = new HandlerList();
                _byMessage.Add(typeof(TMessage), list);
            }

            var entry = new Entry
            {
                Priority = priority,
                Handler = handler,
            };

            if (list.PublishDepth > 0)
            {
                // 発行中の登録は「その発行では呼ばれない」（挙動を決定的にする）
                if (list.Pending == null)
                {
                    list.Pending = new List<Entry>(2);
                }
                list.Pending.Add(entry);
            }
            else
            {
                InsertSorted(list.Items, entry);
            }
            return new Subscription(list, entry);
        }

        /// <summary>
        /// 配達の内部実装。
        /// 購読者ごとに例外を隔離し（後続への配達を必ず続ける）、
        /// 配達後に集約して投げ直す（DeliveryFailed が購読されていれば通知のみ）。
        /// </summary>
        private void PublishInternal<TMessage>(in TMessage message) where TMessage : struct
        {
            EnsureMainThread();

            if (!_byMessage.TryGetValue(typeof(TMessage), out var list) || list.Items.Count == 0)
            {
                MessagePublished?.Invoke(typeof(TMessage), 0);
                return;
            }

            if (_globalPublishDepth >= MaxPublishDepth)
            {
                throw new HubException(
                    $"メッセージ発行の入れ子が上限({MaxPublishDepth})を超えた: {typeof(TMessage).Name}（循環発行を検知）");
            }

            MessagePublished?.Invoke(typeof(TMessage), list.Items.Count);

            var errorsBefore = _deliveryErrors.Count;
            _globalPublishDepth++;
            list.PublishDepth++;
            try
            {
                for (var i = 0; i < list.Items.Count; i++)
                {
                    var entry = list.Items[i];
                    if (entry.Removed)
                    {
                        continue;
                    }
                    try
                    {
                        ((Action<TMessage>)entry.Handler).Invoke(message);
                    }
                    catch (Exception exception)
                    {
                        // 1購読者の例外で他基盤への配達とフレーム処理を止めない
                        if (DeliveryFailed != null)
                        {
                            DeliveryFailed.Invoke(typeof(TMessage), exception);
                        }
                        else
                        {
                            _deliveryErrors.Add(exception);
                        }
                    }
                }
            }
            finally
            {
                list.PublishDepth--;
                _globalPublishDepth--;
                if (list.PublishDepth == 0)
                {
                    if (list.HasTombstone)
                    {
                        list.Items.RemoveAll(e => e.Removed);
                        list.HasTombstone = false;
                    }
                    if (list.Pending != null && list.Pending.Count > 0)
                    {
                        for (var i = 0; i < list.Pending.Count; i++)
                        {
                            if (!list.Pending[i].Removed)
                            {
                                InsertSorted(list.Items, list.Pending[i]);
                            }
                        }
                        list.Pending.Clear();
                    }
                }
            }

            // 最も外側の発行だけが集約例外を投げる（内側で投げると残りの配達が止まる）
            if (_globalPublishDepth == 0 && _deliveryErrors.Count > errorsBefore)
            {
                var aggregate = new AggregateException(_deliveryErrors.ToArray());
                _deliveryErrors.Clear();
                throw new HubException(
                    $"{typeof(TMessage).Name} の配達中に購読者が例外を投げた（配達自体は全員に完了している）", aggregate);
            }
        }

        /// <summary>安定挿入: 優先度昇順・同値は登録順。</summary>
        private static void InsertSorted(List<Entry> items, Entry entry)
        {
            var index = items.Count;
            while (index > 0)
            {
                if (items[index - 1].Priority <= entry.Priority)
                {
                    break;
                }
                index--;
            }
            items.Insert(index, entry);
        }

        /// <summary>メインスレッド専用であることを検査する（開発ビルドのみ）。</summary>
        private void EnsureMainThread()
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                throw new HubException(
                    "MessageHub はメインスレッド専用（ロックを持たない）。別スレッドからは呼べない");
            }
#endif
        }
    }
}
