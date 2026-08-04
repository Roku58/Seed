namespace Seed.Core
{
    /// <summary>
    /// 「巻き戻しに参加する状態持ち」の窓口。
    ///
    /// [なぜこの窓口が必要か]
    /// 巻き戻しに必要な操作がコア側(CoreSnapshot)・購読(EventHub)・レコード件数・
    /// ゲーム状態のコピーへ分散していると、どれか1つを忘れたときに例外ではなく
    /// 「稀に結果がズレる」形で壊れる（＝原因追跡が最も難しい壊れ方）。
    /// 状態を持つ拡張がこのインターフェースを実装しておけば、
    /// LogicContext.CaptureAll / RestoreAll が呼び漏れなく一括で面倒を見る。
    ///
    /// [規約]
    /// - CaptureState が返すトークンは「不透明」。呼び出し側は中身を解釈しない
    ///   （実装側は自前の private クラス/配列を返せばよく、公開型を増やさずに済む）
    /// - Capture 後に元の状態を書き換えても、返したトークンが影響を受けないこと（値の複製）
    /// - RestoreState は自分の CaptureState が返したトークンだけを受け取る
    /// - AddExtension で登録した拡張がこれを実装していれば自動的に参加者になる
    /// </summary>
    public interface ISnapshotParticipant
    {
        /// <summary>現在の状態を不透明なトークンとして取り出す。</summary>
        object CaptureState();

        /// <summary>トークンから状態を復元する。</summary>
        void RestoreState(object state);
    }
}
