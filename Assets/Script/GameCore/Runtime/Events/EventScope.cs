namespace Seed.Core
{
    /// <summary>
    /// イベントの「借りたら自動で返す」スコープ。
    ///
    /// EventPool を直接使うと Rent と Return を毎回対で書く必要があり、
    /// 返し忘れ（＝古いデータ混入・プール枯れ）の温床になる。
    /// using で包むことで、スコープを抜けた瞬間に必ずプールへ返る。
    ///
    /// 使用例:
    /// <code>
    ///   int result;
    ///   using (var scope = EventScope&lt;AttackPowerEvent&gt;.Rent())
    ///   {
    ///       var ev = scope.Event;
    ///       ev.Value = baseValue;
    ///       ctx.Hub.Fire(ev, ctx);
    ///       result = ev.Value; // 結果はスコープ内で読み取る
    ///   } // ← ここで自動返却（例外時も返る）
    /// </code>
    ///
    /// ref struct のためフィールドに保持できず、スコープ外への持ち出しも
    /// コンパイルエラーになる（＝借りっぱなしが構文的に不可能）。
    /// </summary>
    public ref struct EventScope<T> where T : LogicEvent, new()
    {
        /// <summary>借りているイベント（返却済みなら null）。</summary>
        private T _event;

        /// <summary>借りているイベント。Dispose 後は null。</summary>
        public T Event => _event;

        /// <summary>EventScope を生成する。</summary>
        private EventScope(T ev)
        {
            _event = ev;
        }

        /// <summary>プールからイベントを借りる。</summary>
        public static EventScope<T> Rent()
        {
            return new EventScope<T>(EventPool<T>.Rent());
        }

        /// <summary>借りたイベントを自動返却する。</summary>
        public void Dispose()
        {
            if (_event == null)
            {
                return;
            }
            EventPool<T>.Return(_event);
            _event = null; // 二重返却防止
        }
    }
}
