using System;

namespace Seed.Core
{
    /// <summary>
    /// コア内部の動きを覗くためのトレース窓口。
    ///
    /// [目的] 連鎖が「なぜか発動しない」（再入ガードで黙ってスキップされた等）を
    /// 調査できるようにする。LogicContext.TraceListener に設定すると有効になり、
    /// 未設定（null）なら一切コストはかからない。
    ///
    /// 事象レコード（RecordLog）が「ゲームとして何が起きたか」の記録なのに対し、
    /// トレースは「コアがどう動いたか」の記録。開発時専用で、
    /// リリースビルドでは設定しないこと。
    /// </summary>
    public interface ICoreTraceListener
    {
        void OnSectionEnter(string sectionName, int depth);
        void OnSectionExit(string sectionName, int depth);
        void OnEventFired(Type eventType, int subscriberSlots);
        /// <summary>再入ガードによりハンドラーがスキップされた（意図しない連鎖切れの第一容疑者）。</summary>
        void OnHandlerReentrySkipped(ILogicEventHandler handler, Type eventType);
    }
}
