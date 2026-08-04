namespace Seed.Core
{
    /// <summary>ハンドラーの非ジェネリック共通面（EventHub の管理用）。</summary>
    public interface ILogicEventHandler
    {
        /// <summary>
        /// [設計原則: 発火順の決定性]
        /// 同一イベントに複数ハンドラーが反応するときの処理順（小さいほど先、同値は登録順で安定）。
        /// 登録順への暗黙依存を禁止し、ゲーム側で優先度規約クラスを必ず定義すること。
        /// 例: 武器固有=100 → 装備スキル=200 → アイテムバフ=300 → 敵状態=400
        /// </summary>
        int Priority { get; }

        /// <summary>所有者（アクター等）。null 可（フィールド・天候などの無所属仕様）。</summary>
        object Owner { get; }

        /// <summary>再入ガード（EventHub が呼ぶ）。実行中なら false を返す。</summary>
        bool EnterExecution();

        void ExitExecution();
    }

    /// <summary>
    /// 型別購読のハンドラーインターフェース。
    ///
    /// [設計原則: 型別購読]
    /// 「全ハンドラーに全イベントを配って is で判別」ではなく、購読者だけに O(購読者数) で配る。
    /// 1つの仕様が複数イベントに反応する場合は、このインターフェースを複数実装する。
    /// </summary>
    public interface ILogicEventHandler<in TEvent> : ILogicEventHandler where TEvent : LogicEvent
    {
        void Handle(TEvent ev, LogicContext ctx);
    }
}
