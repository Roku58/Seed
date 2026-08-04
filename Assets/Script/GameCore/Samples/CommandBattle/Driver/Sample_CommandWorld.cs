namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】コマンドバトルの「世界」一式（コンテキスト・登場者・技・進行役）。
    /// 合成ルート（このタイトルに何が存在するかの配線）をデモ本体から分離し、1箇所へ集約する。
    /// アクションバトル側の Sample_ActionWorld と対になる構成。
    /// </summary>
    public sealed class Sample_CommandWorld
    {
        /// <summary>実行コンテキスト。</summary>
        public LogicContext Ctx { get; }

        /// <summary>フィールド状態（天気・ターン番号）。</summary>
        public Sample_FieldState Field { get; }

        /// <summary>技→ハンドラーの紐付け（タイトルごとの差し替え点）。</summary>
        public FactoryRegistry<Sample_MoveData, Sample_Actor, LogicEventHandlerBase> Bindings { get; }

        /// <summary>リザードン（攻撃側のデモアクター）。</summary>
        public Sample_Actor Charizard { get; }

        /// <summary>ピカチュウ（せいでんき持ちのデモアクター）。</summary>
        public Sample_Actor Pikachu { get; }

        /// <summary>ほのおのパンチ（接触・追加効果やけど）。</summary>
        public Sample_MoveData FirePunch { get; }

        /// <summary>10まんボルト（非接触の攻撃技）。</summary>
        public Sample_MoveData Thunderbolt { get; }

        /// <summary>まもる（変化技。実体はハンドラーのバインドで決まる）。</summary>
        public Sample_MoveData Protect { get; }

        /// <summary>ターン制の進行役。</summary>
        public Sample_TurnDriver Driver { get; }

        /// <summary>Sample_CommandWorld を生成する（世界の組み立てはこのコンストラクタに集約）。</summary>
        public Sample_CommandWorld(uint seed, ICoreTraceListener trace)
        {
            Field = new Sample_FieldState { Weather = Sample_Weather.Sunny };
            Bindings = new FactoryRegistry<Sample_MoveData, Sample_Actor, LogicEventHandlerBase>();
            Ctx = CreateContext(seed, trace, Field, Bindings);

            Charizard = CreateCharizard();
            Pikachu = CreatePikachu();

            FirePunch = CreateFirePunch();
            Thunderbolt = CreateThunderbolt();
            Protect = CreateProtect();

            RegisterSpecs();

            Driver = new Sample_TurnDriver(Ctx);
        }

        /// <summary>コンテキストを作り、ゲーム固有の拡張（レコードログ・フィールド・バインド表）を登録する。</summary>
        private static LogicContext CreateContext(uint seed, ICoreTraceListener trace,
            Sample_FieldState field,
            FactoryRegistry<Sample_MoveData, Sample_Actor, LogicEventHandlerBase> bindings)
        {
            var ctx = new LogicContext(seed);
            ctx.TraceListener = trace; // ★パターン: トレース（開発時のみ設定）
            ctx.AddExtension(new RecordLog<Sample_Record>());
            ctx.AddExtension(field);
            ctx.AddExtension(bindings);
            return ctx;
        }

        /// <summary>リザードンを生成する。</summary>
        private static Sample_Actor CreateCharizard()
        {
            return new Sample_Actor("リザードン",
                new[] { Sample_Element.Fire, Sample_Element.Flying }, 180, 100, 80, 100);
        }

        /// <summary>ピカチュウを生成する。</summary>
        private static Sample_Actor CreatePikachu()
        {
            return new Sample_Actor("ピカチュウ",
                new[] { Sample_Element.Electric }, 120, 85, 60, 90);
        }

        /// <summary>ほのおのパンチ（接触・10%でやけど）を生成する。</summary>
        private static Sample_MoveData CreateFirePunch()
        {
            return new Sample_MoveData("ほのおのパンチ", Sample_Element.Fire, Sample_MoveCategory.Attack,
                power: 75, accuracyPermille: 1000, makesContact: true,
                secondaryCondition: Sample_ConditionKind.Burn, secondaryChancePermille: 100);
        }

        /// <summary>10まんボルトを生成する。</summary>
        private static Sample_MoveData CreateThunderbolt()
        {
            return new Sample_MoveData("10まんボルト", Sample_Element.Electric, Sample_MoveCategory.Attack,
                power: 90, accuracyPermille: 1000);
        }

        /// <summary>まもる（優先度+4の変化技）を生成する。</summary>
        private static Sample_MoveData CreateProtect()
        {
            return new Sample_MoveData("まもる", Sample_Element.Normal, Sample_MoveCategory.Status,
                power: 0, accuracyPermille: 1000, priority: 4);
        }

        /// <summary>
        /// 合成ルート: このタイトルに登場する個別仕様だけを登録する（未登録＝オミット）。
        /// </summary>
        private void RegisterSpecs()
        {
            new Sample_SunnyWeatherHandler(Field).RegisterTo(Ctx.Hub);
            new Sample_StaticAbilityHandler(Pikachu, chancePermille: 1000).RegisterTo(Ctx.Hub); // デモ用に確定発動
            new Sample_CheriBerryHandler(Charizard).RegisterTo(Ctx.Hub);

            // ターン制では「まもる」= 発動判定に作用するハンドラー（アクション版はダメージ側に作用）
            Bindings.Bind(Protect, owner => new Sample_ProtectTurnHandler(owner));
        }
    }
}
