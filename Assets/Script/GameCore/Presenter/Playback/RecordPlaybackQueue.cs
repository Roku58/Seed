using System;
using System.Collections.Generic;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// ターン制向けの「順次再生キュー」。
    ///
    /// ロジックは一瞬で最後まで計算を終えるが、演出は1件ずつ時間をかけて
    /// 見せたい——そのギャップを埋める部品。レコードを溜め、ステップ（演出）へ
    /// 変換しながら1件ずつ再生する。
    ///
    /// - factory が null を返したレコードは「演出なし」として即時消化される
    /// - SpeedMultiplier で倍速。1フレームに複数ステップまで進む（MaxStepsPerTick が上限）
    /// - SkipAll は「早回し」＝残りを Complete で全消化する（終端状態が全部適用されてからアイドル）。
    ///   演出を本当に捨てたいときだけ DiscardAll を使う
    /// - IsPaused / Drained で「再生中は入力ロック」の定番状態機械を各ゲームで書き直さずに済む
    /// - 巻き戻し（RecordLog.TruncateTo）には AttachRollbackSync で自動追従できる
    /// - 本体は純C#（MonoBehaviour非依存）なので EditMode テストで検証できる
    ///
    /// 使い方（MonoBehaviour側）:
    /// <code>
    ///   _queue.EnqueueFrom(log, ref _cursor);   // 新着を取り込み
    ///   _queue.Tick(Time.deltaTime);            // Updateで回す
    ///   if (_queue.IsIdle) { 入力受付を再開など }
    /// </code>
    /// </summary>
    public sealed class RecordPlaybackQueue<TRecord> where TRecord : struct
    {
        /// <summary>
        /// レコード→再生ステップの変換（ゲーム側の対応表）。null＝演出なし。
        /// ラムダで気軽に書けるよう値渡しにしている（レコードは小さなstructの規約なのでコピーは軽い）。
        /// </summary>
        public delegate IPlaybackStep StepFactory(TRecord record);

        /// <summary>
        /// 再生待ち1件。取り込み元のログ位置を一緒に覚えておく。
        /// 巻き戻し（TruncateTo）で「どこから先を捨てればよいか」を判定するために必要で、
        /// レコード本体だけでは切り詰め済みかどうかが分からない。
        /// </summary>
        private readonly struct PendingEntry
        {
            /// <summary>取り込み元のログ位置。手動 Enqueue は -1（ログ由来でないので巻き戻しの対象外）。</summary>
            public readonly int LogIndex;
            /// <summary>再生待ちのレコード。</summary>
            public readonly TRecord Record;

            /// <summary>PendingEntry を生成する。</summary>
            public PendingEntry(int logIndex, in TRecord record)
            {
                LogIndex = logIndex;
                Record = record;
            }
        }

        /// <summary>手動 Enqueue のログ位置（ログ由来でない印）。</summary>
        private const int ManualLogIndex = -1;

        /// <summary>先頭側の消化済み要素をまとめて捨てる閾値（Listの前詰めコストを抑える）。</summary>
        private const int CompactThreshold = 64;

        /// <summary>レコード→ステップの変換（ゲーム側の対応表）。</summary>
        private readonly StepFactory _factory;

        /// <summary>
        /// 再生待ちのレコード。Queue ではなく List＋読み出し位置にしているのは、
        /// DiscardFrom で途中の要素を条件付きで抜く必要があり、Queue では走査できないため。
        /// </summary>
        private readonly List<PendingEntry> _pending = new List<PendingEntry>(32);

        /// <summary>_pending の読み出し位置（ここより前は消化済み）。</summary>
        private int _head;

        /// <summary>巻き戻し追従用に購読しているログ（未購読なら null）。</summary>
        private RecordLog<TRecord> _syncedLog;

        /// <summary>Truncated 購読に使う固定デリゲート（購読解除で同一参照が必要なため使い回す）。</summary>
        private readonly Action<int> _onLogTruncated;

        /// <summary>再生中のステップ。</summary>
        private IPlaybackStep _current;

        /// <summary>1回の Tick で消化できるステップ数の上限。</summary>
        private int _maxStepsPerTick = 16;

        /// <summary>再生速度（2で倍速）。演出側の都合なのでロジックには影響しない。</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// 一時停止中か。true の間 Tick は何も進めない。
        /// 「ポーズメニュー中は演出を止める」を各ゲームで手書きしないための定番機能。
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// 1回の Tick で消化できるステップ数の上限（既定16）。
        /// 倍速時に残余時間を持ち越すため、放っておくと1フレームで大量のステップを
        /// 消化しうる。暴走（と実質フリーズ）を防ぐ安全弁。1未満は1に丸める。
        /// なお「速すぎて団子に見える」問題はここではなく、ステップ側の最短表示時間で制御する。
        /// </summary>
        public int MaxStepsPerTick
        {
            get => _maxStepsPerTick;
            set => _maxStepsPerTick = value < 1 ? 1 : value;
        }

        /// <summary>
        /// 待機が空になり再生が完了した瞬間に1回だけ発火する。入力ロック解除のフック用。
        /// Tick / SkipAll / DiscardAll で「再生中→アイドル」へ落ちたときに鳴る。
        /// 巻き戻し由来の DiscardFrom では鳴らさない（再生完了ではなく取り消しであり、
        /// ロジックの TruncateTo の途中で入力解放が走ると順序が読めなくなるため）。
        /// アイドル判定そのものは IsIdle のポーリングが正であり、本イベントは通知の便宜。
        /// </summary>
        public event Action Drained;

        /// <summary>再生中でも待機中でもない＝全部見せ終わった。</summary>
        public bool IsIdle => _current == null && PendingCount == 0;

        /// <summary>再生待ち件数。</summary>
        public int PendingCount => _pending.Count - _head;

        /// <summary>RecordPlaybackQueue を生成する。</summary>
        public RecordPlaybackQueue(StepFactory factory)
        {
            _factory = factory;
            // 生成時に1回だけ作り、購読/解除で同じ参照を使う（DiscardFrom は件数を返すためラムダで包む）
            _onLogTruncated = logIndex => DiscardFrom(logIndex);
        }

        /// <summary>再生待ちに追加する（ログ由来でない手動投入。巻き戻しでは破棄されない）。</summary>
        public void Enqueue(in TRecord record)
        {
            _pending.Add(new PendingEntry(ManualLogIndex, in record));
        }

        /// <summary>ログの新着分をまとめて取り込む。取り込んだ件数を返す。</summary>
        public int EnqueueFrom(RecordLog<TRecord> log, ref int cursor)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var count = 0;
            while (true)
            {
                var index = cursor; // TryRead で進む前の位置＝このレコードのログ位置
                if (!log.TryRead(ref cursor, out var record))
                {
                    break;
                }
                _pending.Add(new PendingEntry(index, in record));
                count++;
            }
            return count;
        }

        /// <summary>
        /// 毎フレーム呼ぶ。完了したステップの残余時間は次のステップへ持ち越すので、
        /// 倍速では1フレームに複数ステップを（実時間ぶんだけ）消化できる。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (IsPaused)
            {
                return;
            }

            var wasBusy = !IsIdle;
            var dt = deltaSeconds * SpeedMultiplier;

            for (var steps = 0; steps < _maxStepsPerTick; steps++)
            {
                if (_current == null)
                {
                    if (PendingCount == 0)
                    {
                        break;
                    }

                    _current = _factory(Dequeue().Record);
                    if (_current == null)
                    {
                        continue; // 演出なしレコードは即時消化（ループ回数は消費するので暴走しない）
                    }
                }

                if (!_current.Tick(dt))
                {
                    break; // まだ再生中＝今フレームの時間は使い切った
                }

                var remainder = _current.Remainder;
                dt = remainder > 0f ? remainder : 0f;
                _current = null;
            }

            if (wasBusy && IsIdle)
            {
                Drained?.Invoke();
            }
        }

        /// <summary>
        /// 残りの演出を「早回し」で全部消化する（スキップボタン用）。
        /// 再生中ステップも未再生レコードも Complete で終端状態だけ適用するので、
        /// 「ガード中にスキップしてガードポーズが永久に残る」類の取り残しが起きない。
        /// 状態そのものはレコード時点で確定済みなので、時間を飛ばしても結果は変わらない。
        /// </summary>
        public void SkipAll()
        {
            var wasBusy = !IsIdle;

            if (_current != null)
            {
                var current = _current;
                _current = null;
                current.Complete();
            }

            while (PendingCount > 0)
            {
                var step = _factory(Dequeue().Record);
                step?.Complete();
            }

            if (wasBusy)
            {
                Drained?.Invoke();
            }
        }

        /// <summary>
        /// 残りの演出を終端処理なしで捨てる。
        /// 画面を作り直す前（シーン遷移・リトライ・巻き戻しやり直し）のように
        /// 「終端状態の適用すら不要／有害」な場面のための逃げ道。通常は SkipAll を使う。
        /// </summary>
        public void DiscardAll()
        {
            var wasBusy = !IsIdle;
            _current = null;
            _pending.Clear();
            _head = 0;

            if (wasBusy)
            {
                Drained?.Invoke();
            }
        }

        /// <summary>
        /// logIndex 以上のログ位置から取り込んだ待機レコードを破棄する（巻き戻し追従）。破棄件数を返す。
        /// 再生中ステップは対象にしない（巻き戻しは Drain/取り込みより前に完結させる規約であり、
        /// 切り詰め対象がすでに再生中になることは通常ない。仮に起きても、見せてしまった演出を
        /// 途中で取り消すよりは自然終了させるほうが画面のちらつきが少ない）。
        /// </summary>
        public int DiscardFrom(int logIndex)
        {
            var removed = 0;
            var write = _head;
            for (var i = _head; i < _pending.Count; i++)
            {
                var entry = _pending[i];
                if (entry.LogIndex >= logIndex)
                {
                    removed++;
                    continue; // 切り詰められた（もう存在しない）レコード
                }

                if (write != i)
                {
                    _pending[write] = entry;
                }
                write++;
            }

            if (removed > 0)
            {
                _pending.RemoveRange(write, _pending.Count - write);
            }
            return removed;
        }

        /// <summary>
        /// ログの切り詰めに自動追従する（log.Truncated を購読して DiscardFrom を呼ぶ）。
        /// 手動で DiscardFrom を呼んでもよいが、購読しておけば呼び忘れで
        /// 「もう存在しないレコードの演出」が流れる事故を防げる。
        /// 使い終わったら必ず DetachRollbackSync すること（購読が残ると破棄済みの
        /// キューがログから呼ばれ続ける）。
        /// </summary>
        public void AttachRollbackSync(RecordLog<TRecord> log)
        {
            DetachRollbackSync(); // 二重購読防止
            if (log == null)
            {
                return;
            }
            _syncedLog = log;
            log.Truncated += _onLogTruncated;
        }

        /// <summary>巻き戻し追従の購読を解除する。</summary>
        public void DetachRollbackSync()
        {
            if (_syncedLog == null)
            {
                return;
            }
            _syncedLog.Truncated -= _onLogTruncated;
            _syncedLog = null;
        }

        /// <summary>待機列の先頭を取り出す。消化済み領域は溜まりすぎたら前詰めして解放する。</summary>
        private PendingEntry Dequeue()
        {
            var entry = _pending[_head];
            _head++;

            if (_head == _pending.Count)
            {
                _pending.Clear(); // 全部消化したので位置をリセット（容量は再利用される）
                _head = 0;
            }
            else if (_head >= CompactThreshold)
            {
                _pending.RemoveRange(0, _head);
                _head = 0;
            }
            return entry;
        }
    }
}
