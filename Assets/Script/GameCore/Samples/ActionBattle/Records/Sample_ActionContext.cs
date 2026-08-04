namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】LogicContext 拡張への型付きアクセスヘルパー。</summary>
    public static class Sample_ActionContext
    {
        /// <summary>実況レコードログ拡張を取得する。</summary>
        public static RecordLog<Sample_Record> Log(LogicContext ctx)
        {
            return ctx.GetExtension<RecordLog<Sample_Record>>();
        }

        /// <summary>エンティティ台帳拡張を取得する。</summary>
        public static EntityRegistry Registry(LogicContext ctx)
        {
            return ctx.GetExtension<EntityRegistry>();
        }

        /// <summary>事象レコードを追記する。</summary>
        public static void AddRecord(LogicContext ctx, in Sample_Record record)
        {
            Log(ctx).Add(in record);
        }

        /// <summary>参照→ID変換（未登録なら例外＝合成ルートの登録漏れを早期検知）。</summary>
        public static int Id(LogicContext ctx, object entity)
        {
            return entity == null ? EntityRegistry.None : Registry(ctx).GetId(entity);
        }
    }
}
