namespace Seed.Core
{
    /// <summary>
    /// イベント基底。セクション（ゲームロジック）が計算の要所で発火し、
    /// 個別仕様（イベントハンドラー）が結果に介入するためのポイント。
    ///
    /// [規約]
    /// - イベント型は必ず sealed にする。EventHub は「完全一致の型」でディスパッチし、
    ///   継承によるポリモーフィック配信はサポートしない（暗黙の配信先が増える事故防止）。
    /// - ペイロードは public フィールドで持ち、ハンドラーが補正値を書き込む。
    /// - プールで使い回すため、Reset で参照を確実に切ること（古い参照の混入はバグの温床）。
    /// - 貸し借りは EventScope&lt;T&gt;（using 自動返却）経由を推奨。
    /// </summary>
    public abstract class LogicEvent
    {
        /// <summary>
        /// この発火が「誰宛て」かを表す対象ID（0=全体宛て。EntityRegistry のIDを入れる想定）。
        ///
        /// [なぜ Reset の責務にしないか]
        /// 値は EventHub.Fire(ev, ctx, targetId) が発火のたびに必ず上書きする。
        /// そのため既存イベントの Reset() 実装（プールへ返すときの初期化）に手を入れる必要がなく、
        /// 「Reset が TargetId を戻し忘れて前回の宛先が残る」という事故も原理的に起きない。
        /// ハンドラー側は読み取り専用として扱うこと（書き換えても次の Fire で上書きされる）。
        /// </summary>
        public int TargetId { get; set; }

        /// <summary>プール在庫中か（EventPool の二重返却検知用。ゲーム側は触らない）。</summary>
        internal bool InPool;

        /// <summary>プール返却時に全フィールドを初期化する。</summary>
        public abstract void Reset();
    }
}
