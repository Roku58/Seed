using System;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// 「レコードログを消費カーソルで読み、新着だけを演出へ流す」部分だけを切り出した純C#部品。
    ///
    /// [なぜ MonoBehaviour から分離したか]
    /// - 純C#なので EditMode テストで巻き戻し追従まで検証できる（MonoBehaviour では不能だった）
    /// - 1つのViewが複数ログを購読したり、Presenter以外（実績・テレメトリ）が使い回せる（合成可能）
    /// - 「状態は純C#、MonoBehaviour は写すだけ」というプロジェクト方針に揃える
    /// MonoBehaviour で使いたい場合は RecordPresenterBase が本クラスの薄いラッパになっている。
    ///
    /// [巻き戻し追従の要点]
    /// Attach で log.Truncated を購読し、切り詰めが起きた「その瞬間」にカーソルを丸める。
    /// 事後に Count と比べる方式（SyncAfterRollback）では、切り詰めのあとに追記が入ると
    /// カーソルが Count を超えないため補正が黙ってスキップされ、
    /// 「旧タイムラインの続き位置」から読み始めて新旧が混ざる（＝画面が嘘をつく）。
    /// 切り詰め→追記→切り詰めがどんな順序で来ても混ざらないのは、イベントで即時に丸めるからである。
    ///
    /// 使い終わったら必ず Detach すること（購読が残ると破棄済みの消費者がログから呼ばれ続ける）。
    /// </summary>
    public sealed class RecordDrain<TRecord> where TRecord : struct
    {
        /// <summary>レコード1件を演出へ翻訳する処理。in 渡しでコピーを避ける（低GC規約）。</summary>
        public delegate void DispatchHandler(in TRecord record);

        /// <summary>レコード1件の翻訳先。</summary>
        private readonly DispatchHandler _dispatch;

        /// <summary>
        /// Truncated 購読に使う固定デリゲート。
        /// 生成時に1回だけ作る（Attach/Detach を繰り返しても同じ参照で解除でき、割り当ても増えない）。
        /// </summary>
        private readonly Action<int> _onTruncated;

        /// <summary>読み取り対象のレコードログ。</summary>
        private RecordLog<TRecord> _log;

        /// <summary>どこまで読んだかの位置。</summary>
        private int _cursor;

        /// <summary>RecordDrain を生成する。</summary>
        public RecordDrain(DispatchHandler dispatch)
        {
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            _onTruncated = OnTruncated;
        }

        /// <summary>ログ接続済みか。</summary>
        public bool IsAttached => _log != null;

        /// <summary>どこまで読んだか（テスト・診断用）。</summary>
        public int Cursor => _cursor;

        /// <summary>
        /// 読み取り対象のログを接続する。
        /// consumeExisting=true なら既存レコードも先頭から処理、false なら今後の新着のみ。
        /// </summary>
        public void Attach(RecordLog<TRecord> log, bool consumeExisting = false)
        {
            Detach(); // 別のログへ張り替えるときに購読が二重に残らないようにする
            _log = log;
            if (log == null)
            {
                _cursor = 0;
                return;
            }

            log.Truncated += _onTruncated;
            _cursor = consumeExisting ? 0 : log.Count;
        }

        /// <summary>ログとの接続を解除する（購読も外す）。</summary>
        public void Detach()
        {
            if (_log != null)
            {
                _log.Truncated -= _onTruncated;
            }
            _log = null;
            _cursor = 0;
        }

        /// <summary>新着レコードをすべて処理する。処理した件数を返す。</summary>
        public int Drain()
        {
            if (_log == null)
            {
                return 0;
            }

            var count = 0;
            while (_log.TryRead(ref _cursor, out var record))
            {
                _dispatch(in record);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 巻き戻し（RecordLog.TruncateTo）の後に呼ぶと、カーソルの追い越しを補正する。
        /// Attach 中は Truncated 購読で即時に補正済みなので通常は不要。
        /// 後方互換のための保険（＋ログ側のイベントを通らない経路が将来できた場合の砦）として残す。
        /// </summary>
        public void SyncAfterRollback()
        {
            if (_log != null && _cursor > _log.Count)
            {
                _cursor = _log.Count;
            }
        }

        /// <summary>切り詰めが起きた瞬間にカーソルを丸める（追記より先に必ず通るのが肝）。</summary>
        private void OnTruncated(int count)
        {
            _cursor = Math.Min(_cursor, count);
        }
    }
}
