using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// 事象レコードログ。ゲームロジックの「出力」。
    ///
    /// [設計原則: ログではなく事象レコードの列を出力に]
    /// - ロジックは文字列を組み立てず、型付きの readonly struct レコードだけを積む
    /// - 文字列化・ダメージ数字表示・ヒットストップ・SE/VFX への変換はView層の責務
    /// - ゴールデンログテスト（同一シード→同一レコード列）の比較対象
    /// - レコード型はゲームごとに定義し、LogicContext の拡張として登録して使う
    ///
    /// [教訓] レコードには「その時点の値（HPなど）」をスナップショットで焼き込むこと。
    /// View層が後からアクター実体を読むと最終状態しか見えない（事象レコードは自己完結が原則）。
    ///
    /// [巻き戻しと消費者の追従]
    /// 消費者（Presenter等）はカーソル(int)で「新着分だけ」を読む。巻き戻しで切り詰めが
    /// 起きた事実を検知できないと、旧タイムラインの続きを読んで新旧が混ざる。
    /// そのため TruncateTo / Clear は Truncated イベントで切り詰め後の件数を通知する。
    ///
    /// [成長戦略: 先頭を捨てない]
    /// 長時間セッションでの無限成長対策として「先頭を捨てる」方式は採らない。
    /// TryRead のカーソルは「先頭からの絶対位置」なので、先頭を捨てると
    /// 全消費者のカーソルの意味が静かにズレる（読み飛ばし・二重読みが起きる）。
    /// 代わりに TotalAdded（累計追加数）で成長を観測でき、WarnThreshold で
    /// 「際限なく積み続けている」構成ミスを例外として検知できるようにしている。
    /// 実際に捨てたいゲームは、節目で Clear（＝Truncated 通知つき）を呼ぶこと。
    /// </summary>
    public sealed class RecordLog<TRecord> where TRecord : struct
    {
        /// <summary>レコードの本体。</summary>
        private readonly List<TRecord> _records;

        /// <summary>RecordLog を生成する。</summary>
        public RecordLog(int capacity = 64)
        {
            _records = new List<TRecord>(capacity);
        }

        /// <summary>
        /// 切り詰め通知（引数＝切り詰め後の件数）。TruncateTo / Clear の中で発火する。
        /// 購読側はこの瞬間に自分のカーソルを min(cursor, count) へ丸めればよく、
        /// どんな切り詰め列（何度巻き戻しても）でも正しく追従できる。
        /// </summary>
        public event Action<int> Truncated;

        /// <summary>件数。</summary>
        public int Count => _records.Count;

        /// <summary>切り詰めの世代（TruncateTo / Clear ごとに +1）。診断・アサート用。</summary>
        public int Generation { get; private set; }

        /// <summary>これまでに追加された累計件数（切り詰めても減らない）。成長の観測用。</summary>
        public long TotalAdded { get; private set; }

        /// <summary>
        /// 件数の警戒上限（0=無効が既定）。この件数以上で Add すると LogicException。
        /// 「節目で切り詰める・Clear する」設計を忘れたまま積み続けている構成ミスを、
        /// メモリを食い潰す前に落として気づけるようにするための開発用ガード。
        /// </summary>
        public int WarnThreshold { get; set; }

        public TRecord this[int index] => _records[index];

        /// <summary>値を追加する。</summary>
        public void Add(in TRecord record)
        {
            if (WarnThreshold > 0 && _records.Count >= WarnThreshold)
            {
                throw new LogicException(
                    $"RecordLog の件数が警戒上限({WarnThreshold})に達した（節目での切り詰め・Clear が漏れている）");
            }
            _records.Add(record);
            TotalAdded++;
        }

        /// <summary>内容をすべて消去する（Truncated(0) を通知し、世代を進める）。</summary>
        public void Clear()
        {
            _records.Clear();
            Generation++;
            Truncated?.Invoke(0);
        }

        /// <summary>View層への引き渡し等に。destination はクリアされない（追記）。</summary>
        public void CopyTo(List<TRecord> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }
            for (var i = 0; i < _records.Count; i++)
            {
                destination.Add(_records[i]);
            }
        }

        /// <summary>
        /// 消費カーソル方式の読み取り（★演出Presenter・メタシステム購読用）。
        /// 呼び出し側がカーソル(int)を保持し、毎フレーム「新着分だけ」を読む。
        /// 複数の消費者（演出・実績・テレメトリ）がそれぞれ自分のカーソルで独立に読める。
        /// <code>
        ///   while (log.TryRead(ref _cursor, out var record)) Dispatch(record);
        /// </code>
        /// </summary>
        public bool TryRead(ref int cursor, out TRecord record)
        {
            if ((uint)cursor < (uint)_records.Count)
            {
                record = _records[cursor];
                cursor++;
                return true;
            }
            record = default;
            return false;
        }

        /// <summary>
        /// 指定件数まで切り詰める（巻き戻し用）。
        /// LogicContext.CaptureCoreSnapshot 時点の Count を控えておき、
        /// 巻き戻し時にその件数へ戻す、という使い方をする。
        ///
        /// 切り詰め後に Truncated(count) を発火する（件数が変わらない呼び出しでも
        /// 世代を進めて通知する＝消費者の分岐を減らし、挙動を予測可能に保つ）。
        /// </summary>
        public void TruncateTo(int count)
        {
            if (count < 0 || count > _records.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            _records.RemoveRange(count, _records.Count - count);
            Generation++;
            Truncated?.Invoke(count);
        }
    }
}
