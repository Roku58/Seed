using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// イベントのプール。
    ///
    /// [設計原則: 低GC]
    /// イベントは毎ヒット・毎計算で発火されるため、都度 new しない。
    /// ゲームロジックは1スレッドで回す規約のため、スレッドセーフにはしていない
    /// （プールはスレッドごとに分離され、別スレッドの並列世界とは混ざらない）。
    ///
    /// [コンテキストをまたぐ共有について]
    /// プールは型ごとの静的共有だが、イベントの生存期間は EventScope の using 区間
    /// （＝1発火の間）だけなので、同一スレッドで複数の LogicContext（本世界と先読み用の
    /// 複製世界）を順に回しても状態は混ざらない。借りたまま持ち越さないことが規約。
    ///
    /// [防御]
    /// - 二重返却（同じインスタンスを2回 Return）は同一イベントの共有につながる
    ///   重大事故なので、その場で LogicException にする
    /// - プールの保持数には上限を設け、超過分は捨てる（イベント型が多いゲームでの
    ///   静的保持メモリの際限ない成長を防ぐ）
    ///
    /// 直接 Rent/Return を書くより、using で自動返却される EventScope&lt;T&gt; の使用を推奨。
    /// </summary>
    public static class EventPool<T> where T : LogicEvent, new()
    {
        /// <summary>プールが保持する最大数（超過分は捨ててGCに任せる）。</summary>
        public const int MaxPooled = 64;

        /// <summary>使い回し用のスタック（スレッドごとに分離）。</summary>
        [System.ThreadStatic]
        private static Stack<T> _pool;

        /// <summary>プールからイベントを借りる。</summary>
        public static T Rent()
        {
            var pool = _pool;
            if (pool != null && pool.Count > 0)
            {
                var ev = pool.Pop();
                ev.InPool = false;
                return ev;
            }
            return new T();
        }

        /// <summary>イベントを初期化してプールへ返す。二重返却は共有事故として例外。</summary>
        public static void Return(T ev)
        {
            if (ev.InPool)
            {
                throw new LogicException(
                    $"{typeof(T).Name} が二重返却された（同一イベントの共有は状態汚染を起こす）");
            }
            ev.Reset();
            var pool = _pool ??= new Stack<T>(8);
            if (pool.Count >= MaxPooled)
            {
                return; // 上限超過は捨てる（保持メモリの成長防止）
            }
            ev.InPool = true;
            pool.Push(ev);
        }

        /// <summary>テスト用: 現在プールされている数。</summary>
        public static int PooledCount => _pool?.Count ?? 0;
    }
}
