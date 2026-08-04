using System;
using System.Collections.Generic;
using System.Text;

namespace Seed.Core
{
    /// <summary>
    /// トレースを文字列で溜め込む最小実装。
    /// エディタのデバッグウィンドウやテストの失敗解析からそのまま使える。
    /// （文字列を組み立てるため開発時専用）
    /// </summary>
    public sealed class BufferedCoreTrace : ICoreTraceListener
    {
        /// <summary>溜め込んだトレース行。</summary>
        private readonly List<string> _lines = new List<string>(256);
        /// <summary>文字列組み立て用バッファ。</summary>
        private readonly StringBuilder _sb = new StringBuilder(64);
        /// <summary>行数上限。</summary>
        private readonly int _maxLines;
        /// <summary>上限到達済みか。</summary>
        private bool _overflowed;

        /// <param name="maxLines">行数上限。付けっぱなし運用でメモリが際限なく増えるのを防ぐ。</param>
        public BufferedCoreTrace(int maxLines = 10000)
        {
            _maxLines = maxLines;
        }

        /// <summary>溜め込んだトレース行。</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>内容をすべて消去する。</summary>
        public void Clear()
        {
            _lines.Clear();
            _overflowed = false;
        }

        /// <summary>行数上限を確認し、超過時は打ち切りマーカーを入れる。</summary>
        private bool TryReserveLine()
        {
            if (_lines.Count < _maxLines)
            {
                return true;
            }
            if (!_overflowed)
            {
                _overflowed = true;
                _lines.Add("!! トレース上限到達（以降は記録しない。Clear で再開）");
            }
            return false;
        }

        /// <summary>セクション開始のトレース出力。</summary>
        public void OnSectionEnter(string sectionName, int depth)
        {
            if (!TryReserveLine())
            {
                return;
            }
            _sb.Clear();
            for (var i = 0; i < depth; i++)
            {
                _sb.Append("  ");
            }
            _sb.Append("▼ ").Append(sectionName);
            _lines.Add(_sb.ToString());
        }

        /// <summary>セクション終了のトレース出力。</summary>
        public void OnSectionExit(string sectionName, int depth)
        {
            // 入りだけで十分読めるため既定では何もしない（必要なら実装を変更）
        }

        /// <summary>イベント発火のトレース出力。</summary>
        public void OnEventFired(Type eventType, int subscriberSlots)
        {
            if (!TryReserveLine())
            {
                return;
            }
            _sb.Clear();
            _sb.Append("  event: ").Append(eventType.Name)
               .Append(" (購読槽 ").Append(subscriberSlots).Append(')');
            _lines.Add(_sb.ToString());
        }

        /// <summary>再入スキップのトレース出力。</summary>
        public void OnHandlerReentrySkipped(ILogicEventHandler handler, Type eventType)
        {
            if (!TryReserveLine())
            {
                return;
            }
            _sb.Clear();
            _sb.Append("  !! 再入スキップ: ").Append(handler.GetType().Name)
               .Append(" ← ").Append(eventType.Name);
            _lines.Add(_sb.ToString());
        }
    }
}
