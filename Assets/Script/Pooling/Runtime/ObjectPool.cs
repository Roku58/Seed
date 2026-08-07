using System;
using System.Collections.Generic;

namespace Seed.Pooling
{
    /// <summary>プール規約の違反（二重返却・他所からの返却など）。握り潰さず即座に知らせる。</summary>
    public sealed class PoolException : Exception
    {
        /// <summary>PoolException を生成する。</summary>
        public PoolException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// プールされる側の任意契約（貸出・返却の通知を受け取りたいときに実装する）。
    ///
    /// 使い回しの事故は「前回の状態が残っていること」から起きる。返却時に自分で
    /// 状態を捨てる場所を型として持たせておけば、リセット漏れをレビューで見つけやすい。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>プールから貸し出された直後に呼ばれる（初期化はここ）。</summary>
        void OnRent();

        /// <summary>プールへ返される直前に呼ばれる（状態のリセットはここ）。</summary>
        void OnReturn();
    }

    /// <summary>プールの統計（調整とリークの発見に使う）。</summary>
    public readonly struct PoolStats
    {
        /// <summary>今までに新規生成した総数。</summary>
        public readonly int Created;

        /// <summary>現在貸出中の数。</summary>
        public readonly int Rented;

        /// <summary>待機中（プールに寝ている）数。</summary>
        public readonly int Idle;

        /// <summary>同時貸出数の最大値（プールの必要量の目安）。</summary>
        public readonly int PeakRented;

        /// <summary>上限超過で捨てた数（多いなら上限が小さすぎる）。</summary>
        public readonly int Discarded;

        /// <summary>PoolStats を生成する。</summary>
        public PoolStats(int created, int rented, int idle, int peakRented, int discarded)
        {
            Created = created;
            Rented = rented;
            Idle = idle;
            PeakRented = peakRented;
            Discarded = discarded;
        }

        /// <summary>デバッグ表示（エディタ・ログ用）。</summary>
        public override string ToString()
        {
            return $"生成{Created} 貸出{Rented} 待機{Idle} ピーク{PeakRented} 破棄{Discarded}";
        }
    }

    /// <summary>
    /// 汎用オブジェクトプール（純C#）。
    ///
    /// [なぜ自作するか] 標準の <c>UnityEngine.Pool.ObjectPool</c> でも足りる場面は多いが、
    /// 本基盤では次の3点を型で守りたいため自前にしている:
    /// - **二重返却を即例外にする**（同じインスタンスが2箇所で使われる事故は
    ///   発生時点から離れた場所で症状が出るため、その場で落とすのが安い）
    /// - **統計を持つ**（ピーク同時使用数と破棄数が見えないとプール量を決められない）
    /// - **貸出/返却の通知を <see cref="IPoolable"/> として型に載せる**（リセット漏れ対策）
    /// 既存の <c>EventPool</c>（GameCore）と同じ「上限超過は捨てる」方針を引き継いでいる。
    ///
    /// スレッド安全ではない（ゲームループは1スレッドで回す規約）。
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        /// <summary>新規生成の工場。</summary>
        private readonly Func<T> _factory;

        /// <summary>貸出時の追加処理（任意）。</summary>
        private readonly Action<T> _onRent;

        /// <summary>返却時の追加処理（任意）。</summary>
        private readonly Action<T> _onReturn;

        /// <summary>待機中のインスタンス。</summary>
        private readonly Stack<T> _idle;

        /// <summary>貸出中のインスタンス（二重返却・他所からの返却の検知に使う）。</summary>
        private readonly HashSet<T> _rented;

        /// <summary>累計生成数。</summary>
        private int _created;

        /// <summary>同時貸出数のピーク。</summary>
        private int _peakRented;

        /// <summary>上限超過で捨てた数。</summary>
        private int _discarded;

        /// <summary>プールが待機として保持する上限（超過分は捨てる）。</summary>
        public int MaxRetained { get; }

        /// <summary>
        /// ObjectPool を生成する。
        /// onRent / onReturn は <see cref="IPoolable"/> の通知に**加えて**呼ばれる。
        /// </summary>
        public ObjectPool(Func<T> factory, int maxRetained = 64,
            Action<T> onRent = null, Action<T> onReturn = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            if (maxRetained < 0)
            {
                throw new PoolException($"保持上限に負値は指定できない: {maxRetained}");
            }
            MaxRetained = maxRetained;
            _onRent = onRent;
            _onReturn = onReturn;
            _idle = new Stack<T>(Math.Min(maxRetained, 16));
            _rented = new HashSet<T>();
        }

        /// <summary>現在の統計。</summary>
        public PoolStats Stats =>
            new PoolStats(_created, _rented.Count, _idle.Count, _peakRented, _discarded);

        /// <summary>あらかじめ生成しておく（読み込み時に確保して実行中の生成を避ける）。</summary>
        public void Prewarm(int count)
        {
            for (var i = 0; i < count && _idle.Count < MaxRetained; i++)
            {
                _idle.Push(Create());
            }
        }

        /// <summary>1つ借りる（待機が無ければ新規生成する）。</summary>
        public T Rent()
        {
            var item = _idle.Count > 0 ? _idle.Pop() : Create();
            _rented.Add(item);
            if (_rented.Count > _peakRented)
            {
                _peakRented = _rented.Count;
            }
            if (item is IPoolable poolable)
            {
                poolable.OnRent();
            }
            _onRent?.Invoke(item);
            return item;
        }

        /// <summary>
        /// 返す。二重返却・このプール以外からの返却はその場で例外にする
        /// （症状が出る場所と原因が離れる事故なので、発生点で落とす）。
        /// </summary>
        public void Return(T item)
        {
            if (item == null)
            {
                throw new PoolException("null をプールへ返却できない");
            }
            if (!_rented.Remove(item))
            {
                throw new PoolException(
                    $"{typeof(T).Name} の不正な返却（二重返却、またはこのプールが貸したものではない）");
            }
            if (item is IPoolable poolable)
            {
                poolable.OnReturn();
            }
            _onReturn?.Invoke(item);

            if (_idle.Count >= MaxRetained)
            {
                _discarded++;
                return; // 保持上限超過は捨てる（際限ないメモリ成長を防ぐ）
            }
            _idle.Push(item);
        }

        /// <summary>
        /// 待機中を全部捨てる（dispose を渡すと1件ずつ後始末できる）。
        /// 貸出中のものには触らない——所有者が返すまではそちらのもの。
        /// </summary>
        public void Clear(Action<T> dispose = null)
        {
            while (_idle.Count > 0)
            {
                var item = _idle.Pop();
                dispose?.Invoke(item);
            }
        }

        /// <summary>貸出中か（デバッグ・検証用）。</summary>
        public bool IsRented(T item)
        {
            return item != null && _rented.Contains(item);
        }

        /// <summary>新規生成（統計つき）。</summary>
        private T Create()
        {
            var item = _factory();
            if (item == null)
            {
                throw new PoolException($"{typeof(T).Name} の生成工場が null を返した");
            }
            _created++;
            return item;
        }
    }
}
