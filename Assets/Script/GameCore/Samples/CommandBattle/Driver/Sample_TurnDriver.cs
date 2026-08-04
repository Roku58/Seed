namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】ターン制の進行役。「誰が何をするか」を受け取り、行動実行セクションへ流す。
    /// 優先度→素早さの並べ替えは LINQ を使わない挿入ソート（低GC規約）。
    /// </summary>
    public sealed class Sample_TurnDriver
    {
        /// <summary>実行コンテキスト。</summary>
        private readonly LogicContext _ctx;

        /// <summary>Sample_TurnDriver を生成する。</summary>
        public Sample_TurnDriver(LogicContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>1ターン分の行動を実行する。</summary>
        public void RunTurn(params Sample_TurnAction[] actions)
        {
            SortByPriorityThenSpeed(actions);

            for (var i = 0; i < actions.Length; i++)
            {
                if (actions[i].User.IsFainted)
                {
                    continue;
                }

                _ctx.BeginResolution(); // ★1解決ごとの暴走ガードをリセット
                _ctx.RunSection(Sample_ActionExecutionSection.Instance,
                    new Sample_MoveInput(actions[i].User, actions[i].Target, actions[i].Move));
            }

            using (var scope = EventScope<Sample_TurnEndEvent>.Rent())
            {
                _ctx.Hub.Fire(scope.Event, _ctx);
            }

            Sample_CommandContext.AddRecord(_ctx, new Sample_Record(
                Sample_CommandContext.Field(_ctx).Turn, Sample_RecordKind.TurnEnd));
            Sample_CommandContext.Field(_ctx).Turn++;
        }

        /// <summary>優先度→素早さの降順・安定ソート（挿入ソート）。</summary>
        private static void SortByPriorityThenSpeed(Sample_TurnAction[] actions)
        {
            for (var i = 1; i < actions.Length; i++)
            {
                var current = actions[i];
                var j = i - 1;
                while (j >= 0 && ComesAfter(in actions[j], in current))
                {
                    actions[j + 1] = actions[j];
                    j--;
                }
                actions[j + 1] = current;
            }
        }

        /// <summary>並べ替え比較（a を b の後ろへ置くべきか）。</summary>
        private static bool ComesAfter(in Sample_TurnAction a, in Sample_TurnAction b)
        {
            if (a.Move.Priority != b.Move.Priority)
            {
                return a.Move.Priority < b.Move.Priority;
            }
            return a.User.Speed < b.User.Speed;
        }
    }
}
