namespace Seed.Core
{
    /// <summary>
    /// ハンドラー実装の共通基底。
    ///
    /// [設計原則: ハンドラーの状態レス化]
    /// 派生クラスは「不変の設定値」だけを持つこと。効果時間・蓄積などの可変状態は
    /// アクター側（ConditionSet 等）に置く。セーブ・巻き戻し・同期の対象を
    /// アクター状態に一元化するための規約。
    ///
    /// 登録はリフレクションを使わず、RegisterTo で購読を明示する
    /// （その仕様がどのイベントに反応するかがコード上で一目で分かる）。
    /// </summary>
    public abstract class LogicEventHandlerBase : ILogicEventHandler
    {
        /// <summary>再入ガード用の実行中フラグ。</summary>
        private bool _isExecuting;

        /// <summary>所有者（無所属なら null）。</summary>
        public object Owner { get; }
        /// <summary>ハンドラーの適用順（小さいほど先）。</summary>
        public virtual int Priority => 0;

        /// <summary>LogicEventHandlerBase を生成する。</summary>
        protected LogicEventHandlerBase(object owner)
        {
            Owner = owner;
        }

        /// <summary>反応するイベントの購読をここで明示する。</summary>
        public abstract void RegisterTo(EventHub hub);

        /// <summary>再入ガードの開始（EventHubが呼ぶ）。</summary>
        bool ILogicEventHandler.EnterExecution()
        {
            if (_isExecuting)
            {
                return false;
            }
            _isExecuting = true;
            return true;
        }

        /// <summary>再入ガードの終了（EventHubが呼ぶ）。</summary>
        void ILogicEventHandler.ExitExecution()
        {
            _isExecuting = false;
        }
    }
}
