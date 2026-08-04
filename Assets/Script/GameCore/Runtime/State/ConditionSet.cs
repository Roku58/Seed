using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// アクターが持つコンディション集合。
    /// 走査は線形（想定件数は少数）。失効処理は進行役が RemoveExpired で明示的に行う。
    /// </summary>
    public sealed class ConditionSet<TKind> where TKind : struct, Enum
    {
        /// <summary>enum比較用の共有コンパレータ（boxing回避）。</summary>
        private static readonly EqualityComparer<TKind> KindComparer = EqualityComparer<TKind>.Default;

        /// <summary>要素の本体。</summary>
        private readonly List<TimedCondition<TKind>> _items = new List<TimedCondition<TKind>>(4);

        /// <summary>件数。</summary>
        public int Count => _items.Count;

        /// <summary>値を追加する。</summary>
        public void Add(in TimedCondition<TKind> condition)
        {
            _items.Add(condition);
        }

        /// <summary>
        /// 重ねがけポリシー付きの付与。
        /// 同種の有効なコンディションが無ければ単純に追加し、true を返す。
        /// あればポリシーに従ってマージし、内容が変化したときのみ true を返す。
        /// </summary>
        public bool AddOrMerge(in TimedCondition<TKind> condition, ConditionMergePolicy policy, long nowMs)
        {
            var index = -1;
            for (var i = 0; i < _items.Count; i++)
            {
                if (KindComparer.Equals(_items[i].Kind, condition.Kind) && _items[i].ExpiresAtMs > nowMs)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                _items.Add(condition);
                return true;
            }

            var existing = _items[index];
            switch (policy)
            {
                case ConditionMergePolicy.Stack:
                    _items.Add(condition);
                    return true;

                case ConditionMergePolicy.Extend:
                    _items[index] = new TimedCondition<TKind>(existing.Kind,
                        Math.Max(existing.ExpiresAtMs, condition.ExpiresAtMs), existing.Magnitude);
                    return condition.ExpiresAtMs > existing.ExpiresAtMs;

                case ConditionMergePolicy.Overwrite:
                    _items[index] = condition;
                    return true;

                case ConditionMergePolicy.AddMagnitude:
                    _items[index] = new TimedCondition<TKind>(existing.Kind,
                        Math.Max(existing.ExpiresAtMs, condition.ExpiresAtMs),
                        existing.Magnitude + condition.Magnitude);
                    return true;

                case ConditionMergePolicy.Ignore:
                default:
                    return false;
            }
        }

        /// <summary>指定種別の有効なコンディションを取得（複数ある場合は先に付与されたもの）。</summary>
        public bool TryGet(TKind kind, long nowMs, out TimedCondition<TKind> condition)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (KindComparer.Equals(_items[i].Kind, kind) && _items[i].ExpiresAtMs > nowMs)
                {
                    condition = _items[i];
                    return true;
                }
            }
            condition = default;
            return false;
        }

        /// <summary>Has を生成する。</summary>
        public bool Has(TKind kind, long nowMs)
        {
            return TryGet(kind, nowMs, out _);
        }

        /// <summary>指定種別をすべて取り除く（治療・解除）。取り除いた数を返す。</summary>
        public int Remove(TKind kind)
        {
            var removed = 0;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (KindComparer.Equals(_items[i].Kind, kind))
                {
                    _items.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 失効したコンディションを取り除く。失効レコードの発行はゲーム側の責務のため、
        /// 取り除いたものを expiredOut（null可）へ書き出す。
        /// </summary>
        public int RemoveExpired(long nowMs, List<TimedCondition<TKind>> expiredOut)
        {
            var removed = 0;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].ExpiresAtMs > nowMs)
                {
                    continue;
                }

                expiredOut?.Add(_items[i]);
                _items.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// destination の内容を自分の内容で置き換える（巻き戻し用スナップショットの取得・復元）。
        /// 取得: current.CopyTo(saved) / 復元: saved.CopyTo(current)
        /// </summary>
        public void CopyTo(ConditionSet<TKind> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            destination._items.Clear();
            destination._items.AddRange(_items);
        }
    }
}
