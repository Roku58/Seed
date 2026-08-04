// ============================================================================
// 【サンプルコード】Sample_ActionBattleRunner
// アクションバトルのデモ実行＋★パターン: RecordPresenterBase（即時Dispatch型のPresenter）。
//
// Runner自身が RecordPresenterBase<Sample_Record> を継承し、
// Dispatch に「レコード→表示」の対応表を書いている。
// 実プロジェクトでは Debug.Log の部分が UI・キャラSM・カメラ・SE への振り分けになる。
// ============================================================================

using Seed.Core.Presenter;
using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】アクションバトルのデモ実行用ランナー兼Presenter。</summary>
    public sealed class Sample_ActionBattleRunner : RecordPresenterBase<Sample_Record>
    {
        /// <summary>ロジック乱数のシード（同じシードなら結果は完全一致）。</summary>
        [Tooltip("ロジック乱数のシード。同じシードなら結果は完全に一致する（決定性）")]
        [SerializeField] private uint _logicSeed = Sample_ActionBattleDemo.DefaultSeed;

        /// <summary>コアトレースを有効にして内部動作も出力するか。</summary>
        [Tooltip("コアトレース（★パターン: TraceListener）を有効にして内部動作も出力する")]
        [SerializeField] private bool _enableCoreTrace;

        /// <summary>ID解決用のエンティティ台帳。</summary>
        private EntityRegistry _registry;

        /// <summary>起動時にデモを実行する。</summary>
        private void Start()
        {
            Debug.Log("======== Sample_ActionBattle（アクションバトル）デモ ========");
            var trace = _enableCoreTrace ? new BufferedCoreTrace() : null;
            var ctx = Sample_ActionBattleDemo.RunDemo(_logicSeed, trace);
            _registry = ctx.GetExtension<EntityRegistry>();

            // ★Presenterの接続: 既存レコードも先頭から処理する
            Attach(ctx.GetExtension<RecordLog<Sample_Record>>(), consumeExisting: true);
            Drain(); // このデモは全レコードが確定済みなので即時に全件処理（通常はLateUpdateの自動Drainに任せる）

            // ★パターン: リプレイ検証（記録した入力列＋シードから再計算し、構造化ハッシュを突き合わせる）
            var match = Sample_ActionBattleDemo.VerifyReplay(ctx, out var originalHash, out var replayHash);
            Debug.Log(match
                ? $"リプレイ検証 OK: hash={originalHash:X8}（入力列＋シードだけで完全再現できた）"
                : $"リプレイ検証 NG: original={originalHash:X8} replay={replayHash:X8}（決定性が壊れている！）");

            if (trace != null)
            {
                Debug.Log($"---- コアトレース（{trace.Lines.Count}行）----");
                for (var i = 0; i < trace.Lines.Count; i++)
                {
                    Debug.Log(trace.Lines[i]);
                }
            }
        }

        /// <summary>レコード→表示の対応表。実プロジェクトではここがUI・キャラ・カメラへの振り分けになる。</summary>
        protected override void Dispatch(in Sample_Record record)
        {
            Debug.Log(Sample_ActionBattlePresenter.Format(record, _registry));
        }
    }
}
