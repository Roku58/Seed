using System;
using System.Collections.Generic;

namespace Seed.App
{
    /// <summary>
    /// 合成ルートが生成した破棄対象（購読・System・Bridge…）の台帳。
    /// 生成順に Own で預け、Dispose で逆順に片付ける——
    /// 「初期化の逆順で破棄する」というライフサイクル規約を型で保証し、
    /// OnDestroy に手書きの Dispose 羅列が並ぶこと（と、その書き漏れ）を防ぐ。
    /// シーン/フェーズが増えても「1フェーズ=1スコープ」で同じ骨格を再利用できる。
    /// </summary>
    public sealed class CompositionScope : IDisposable
    {
        /// <summary>預かった破棄対象（生成順）。</summary>
        private readonly List<IDisposable> _owned = new List<IDisposable>(8);

        /// <summary>破棄済みか。</summary>
        private bool _isDisposed;

        /// <summary>破棄対象を預ける（生成した場所でそのまま包める糖衣）。</summary>
        public T Own<T>(T disposable) where T : class, IDisposable
        {
            if (disposable == null)
            {
                throw new ArgumentNullException(nameof(disposable));
            }
            if (_isDisposed)
            {
                disposable.Dispose();
                return disposable;
            }
            _owned.Add(disposable);
            return disposable;
        }

        /// <summary>預かった全対象を生成の逆順で破棄する（二重Disposeは無害）。</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            for (var i = _owned.Count - 1; i >= 0; i--)
            {
                _owned[i].Dispose();
            }
            _owned.Clear();
        }
    }
}
