using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// 入力ジャーナル（リプレイの土台）。
    ///
    /// この基盤は決定的（同じシード＋同じ入力列→必ず同じ結果）なので、
    /// リプレイは「プレイヤーの入力」と「シード」さえ残せば成立する。
    /// 進行役（ドライバー）が入力を受け取るたびに Record し、
    /// 再生時は先頭から順に同じ進行役へ流し込むだけでよい。
    ///
    /// - TInput はゲーム側が定義する入力struct（例: 誰が・どの技を・どの対象に）
    /// - バイト列への保存形式はゲーム側の責務（TInput の中身を知らないため）
    /// - Version はゲームロジックの版数。版が違うリプレイの再生を弾くために使う
    /// </summary>
    public sealed class InputJournal<TInput> where TInput : struct
    {
        /// <summary>記録1件（時刻＋入力）。</summary>
        public readonly struct Entry
        {
            /// <summary>入力時点のロジック時刻（ms）。ターン制ではターン番号などでもよい。</summary>
            public readonly long TimeMs;
            /// <summary>入力の中身。</summary>
            public readonly TInput Input;

            /// <summary>Entry を生成する。</summary>
            public Entry(long timeMs, in TInput input)
            {
                TimeMs = timeMs;
                Input = input;
            }
        }

        /// <summary>記録の本体。</summary>
        private readonly List<Entry> _entries;

        /// <summary>このジャーナルとペアになるロジック乱数のシード。</summary>
        public uint Seed { get; set; }

        /// <summary>ロジックの版数（数式・データが変わったら上げる）。</summary>
        public int Version { get; set; }

        /// <summary>件数。</summary>
        public int Count => _entries.Count;

        public Entry this[int index] => _entries[index];

        /// <summary>InputJournal を生成する。</summary>
        public InputJournal(int capacity = 256)
        {
            _entries = new List<Entry>(capacity);
        }

        /// <summary>入力を時刻付きで記録する。</summary>
        public void Record(long timeMs, in TInput input)
        {
            if (_entries.Count > 0 && _entries[_entries.Count - 1].TimeMs > timeMs)
            {
                throw new ArgumentException("入力の時刻が過去に戻っている（記録順は時刻昇順であること）");
            }
            _entries.Add(new Entry(timeMs, in input));
        }

        /// <summary>内容をすべて消去する。</summary>
        public void Clear()
        {
            _entries.Clear();
        }
    }
}
