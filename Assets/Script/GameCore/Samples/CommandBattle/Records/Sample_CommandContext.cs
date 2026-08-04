namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】LogicContext 拡張への型付きアクセスヘルパー。
    /// GetExtension をホットパスで連打しないよう、アクセスをここに集約する。
    /// </summary>
    public static class Sample_CommandContext
    {
        /// <summary>実況レコードログ拡張を取得する。</summary>
        public static RecordLog<Sample_Record> Log(LogicContext ctx)
        {
            return ctx.GetExtension<RecordLog<Sample_Record>>();
        }

        /// <summary>フィールド状態拡張を取得する。</summary>
        public static Sample_FieldState Field(LogicContext ctx)
        {
            return ctx.GetExtension<Sample_FieldState>();
        }

        /// <summary>技→ハンドラーの紐付け（★パターン: FactoryRegistry＝タイトルごとの差し替え点）。</summary>
        public static FactoryRegistry<Sample_MoveData, Sample_Actor, LogicEventHandlerBase> Bindings(LogicContext ctx)
        {
            return ctx.GetExtension<FactoryRegistry<Sample_MoveData, Sample_Actor, LogicEventHandlerBase>>();
        }

        /// <summary>事象レコードを追記する。</summary>
        public static void AddRecord(LogicContext ctx, in Sample_Record record)
        {
            Log(ctx).Add(in record);
        }
    }
}
