using UnityEngine;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// Presenterの基底（ロジック→演出の「唯一の橋」の定型部分）。
    ///
    /// ゲーム側は TRecord を確定させた具象クラスを作り、Dispatch に
    /// 「RecordKind → UI・キャラ・カメラ・SE」の対応表(switch)を書くだけでよい。
    ///
    /// - カーソル方式: 毎フレーム「新着レコードだけ」を読む（RecordLog.TryRead）
    /// - 既定では LateUpdate で自動 Drain（解決は Update 中に終わっている前提＝同フレーム反映）
    /// - 即時反映したい場合は解決直後に手動で Drain() を呼んでもよい
    /// - 消費者は複数共存できる（UI・実績・テレメトリがそれぞれ自分のカーソルを持つ）
    ///
    /// [設計] 実体は純C#の RecordDrain で、本クラスはそれを MonoBehaviour の
    /// ライフサイクル（LateUpdate / OnDestroy）へ繋ぐだけの薄いラッパ。
    /// 状態を純C#側に置くことで、巻き戻し追従などの振る舞いを EditMode テストで検証できる。
    /// 「1インスタンス＝1ログ」の制約も、RecordDrain を直接複数持てば回避できる。
    ///
    /// [巻き戻しとの整合] お試し実行→TruncateTo は「Drainされる前」に完結させる規約
    /// （解決の節目でDrainしていれば自然に守られる）。加えて Attach 中は
    /// RecordLog.Truncated を購読しており、切り詰めの瞬間にカーソルが丸められるので
    /// 「切り詰め→追記」の順で来ても新旧タイムラインは混ざらない。
    /// </summary>
    public abstract class RecordPresenterBase<TRecord> : MonoBehaviour where TRecord : struct
    {
        /// <summary>LateUpdateで自動的に新着レコードを処理するか。</summary>
        [Tooltip("LateUpdateで自動的に新着レコードを処理する（OFFなら手動でDrainを呼ぶ）")]
        [SerializeField] private bool _autoDrainInLateUpdate = true;

        /// <summary>読み取りの実体（純C#）。Dispatch を束ねるため遅延生成する。</summary>
        private RecordDrain<TRecord> _drain;

        /// <summary>ログ接続済みか。</summary>
        public bool IsAttached => _drain != null && _drain.IsAttached;

        /// <summary>
        /// 読み取り対象のログを接続する。
        /// consumeExisting=true なら既存レコードも先頭から処理、false なら今後の新着のみ。
        /// </summary>
        public void Attach(RecordLog<TRecord> log, bool consumeExisting = false)
        {
            EnsureDrain().Attach(log, consumeExisting);
        }

        /// <summary>ログとの接続を解除する。</summary>
        public void Detach()
        {
            _drain?.Detach();
        }

        /// <summary>新着レコードをすべて処理する。処理した件数を返す。</summary>
        public int Drain()
        {
            return _drain == null ? 0 : _drain.Drain();
        }

        /// <summary>巻き戻し（RecordLog.TruncateTo）の後に呼ぶと、カーソルの追い越しを補正する。</summary>
        public void SyncAfterRollback()
        {
            _drain?.SyncAfterRollback();
        }

        /// <summary>毎フレーム、新着レコードを自動処理する。</summary>
        private void LateUpdate()
        {
            if (_autoDrainInLateUpdate)
            {
                Drain();
            }
        }

        /// <summary>破棄時に購読を外す（残すとログから破棄済みオブジェクトの Dispatch が呼ばれる）。</summary>
        private void OnDestroy()
        {
            Detach();
        }

        /// <summary>実体を必要になった時点で生成する（MonoBehaviourのコンストラクタ相当を避けるため）。</summary>
        private RecordDrain<TRecord> EnsureDrain()
        {
            return _drain ??= new RecordDrain<TRecord>(Dispatch);
        }

        /// <summary>1件のレコードを演出へ翻訳する（ゲーム側が実装する対応表）。</summary>
        protected abstract void Dispatch(in TRecord record);
    }
}
