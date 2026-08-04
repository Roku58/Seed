// ============================================================================
// 【サンプルコード】Sample_CommandBattleRunner
// コマンドバトルのデモ実行＋★パターン: RecordPlaybackQueue（順次再生型のPresenter）。
//
// ロジックは Start の一瞬で最後まで確定し、演出（ここではログ表示）だけが
// 1件ずつ時間をかけて後から再生される——ターン制の正しい構成をそのまま実演する。
// Inspector の Playback Interval / Speed で再生間隔・倍速を変えられる
// （ロジック結果には一切影響しない、が体感できる）。
// ============================================================================

using Seed.Core.Presenter;
using Seed.Core.Samples.CommandBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】コマンドバトルのデモ実行用ランナー（順次再生）。</summary>
    public sealed class Sample_CommandBattleRunner : MonoBehaviour
    {
        /// <summary>ロジック乱数のシード（同じシードなら結果は完全一致）。</summary>
        [Tooltip("ロジック乱数のシード。同じシードなら結果は完全に一致する（決定性）")]
        [SerializeField] private uint _logicSeed = Sample_CommandBattleDemo.DefaultSeed;

        /// <summary>コアトレースを有効にして内部動作も出力するか。</summary>
        [Tooltip("コアトレース（★パターン: TraceListener）を有効にして内部動作も出力する")]
        [SerializeField] private bool _enableCoreTrace;

        /// <summary>1レコードあたりの表示間隔（秒）。演出側の都合であり結果には影響しない。</summary>
        [Tooltip("1レコードあたりの表示間隔（秒）。演出側の都合であり結果には影響しない")]
        [SerializeField] private float _playbackInterval = 0.4f;

        /// <summary>再生速度倍率（2で倍速）。</summary>
        [Tooltip("再生速度（2で倍速）")]
        [SerializeField] private float _playbackSpeed = 1f;

        /// <summary>順次再生キュー。</summary>
        private RecordPlaybackQueue<Sample_Record> _queue;
        /// <summary>再生完了ログを出したか。</summary>
        private bool _completedLogged;

        /// <summary>起動時にデモを実行する。</summary>
        private void Start()
        {
            Debug.Log("======== Sample_CommandBattle（コマンドバトル）デモ ========");
            Debug.Log("（結果は一瞬で確定済み。以下は順次再生キューによる演出です）");

            var trace = _enableCoreTrace ? new BufferedCoreTrace() : null;
            var ctx = Sample_CommandBattleDemo.RunDemo(_logicSeed, trace); // ← ロジックはここで完了

            // ★Presenter: レコード→再生ステップの対応表を渡してキューを作り、全レコードを取り込む
            _queue = new RecordPlaybackQueue<Sample_Record>(CreateStep);
            var cursor = 0;
            _queue.EnqueueFrom(ctx.GetExtension<RecordLog<Sample_Record>>(), ref cursor);

            if (trace != null)
            {
                Debug.Log($"---- コアトレース（{trace.Lines.Count}行）----");
                for (var i = 0; i < trace.Lines.Count; i++)
                {
                    Debug.Log(trace.Lines[i]);
                }
            }
        }

        /// <summary>毎フレーム、再生キューを進める。</summary>
        private void Update()
        {
            if (_queue == null)
            {
                return;
            }

            _queue.SpeedMultiplier = _playbackSpeed;
            _queue.Tick(Time.deltaTime);

            if (!_completedLogged && _queue.IsIdle)
            {
                _completedLogged = true;
                Debug.Log("（再生完了。2ターン目の前の『お試しターン』は巻き戻し済みのため、ログに痕跡が無いのが正常）");
            }
        }

        /// <summary>
        /// レコード→再生ステップの対応表。
        /// ここでは全種別「表示して一定時間待つ」だけだが、実プロジェクトでは
        /// Kind ごとにアニメーション・カメラカット等のステップを返す。
        /// </summary>
        private IPlaybackStep CreateStep(Sample_Record record)
        {
            var line = Sample_CommandBattlePresenter.Format(record); // 事前に整形（クロージャに文字列だけ捕捉）
            return new TimedStep(_playbackInterval, onStart: () => Debug.Log(line));
        }
    }
}
