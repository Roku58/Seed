namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】1回の狩りの「世界」一式（コンテキスト・登場者・モーション表・進行役）。
    /// 通常実行とリプレイ再生が全く同じ構築手順を通るよう、生成を1箇所に集約する。
    /// </summary>
    public sealed class Sample_ActionWorld
    {
        /// <summary>実行コンテキスト。</summary>
        public LogicContext Ctx { get; }
        /// <summary>エンティティ台帳。</summary>
        public EntityRegistry Registry { get; }
        /// <summary>ハンター。</summary>
        public Sample_Hunter Hunter { get; }
        /// <summary>モンスター。</summary>
        public Sample_Monster Monster { get; }
        /// <summary>頭部位。</summary>
        public Sample_MonsterPart Head { get; }
        /// <summary>翼部位。</summary>
        public Sample_MonsterPart Wing { get; }
        /// <summary>進行役。</summary>
        public Sample_ActionDriver Driver { get; }

        /// <summary>斬り上げモーション。</summary>
        public Sample_AttackMove SlashUp { get; }
        /// <summary>溜め斬りモーション。</summary>
        public Sample_AttackMove ChargedSlash { get; }
        /// <summary>尻尾回転モーション。</summary>
        public Sample_AttackMove TailSwipe { get; }

        /// <summary>Sample_ActionWorld を生成する。</summary>
        public Sample_ActionWorld(uint seed, ICoreTraceListener trace)
        {
            Ctx = new LogicContext(seed);
            Ctx.TraceListener = trace;
            Ctx.AddExtension(new RecordLog<Sample_Record>());
            Registry = new EntityRegistry();
            Ctx.AddExtension(Registry);

            var weapon = new Sample_WeaponData("爆鱗の剣", attack: 100, affinityPermille: 0,
                status: Sample_StatusKind.Blast, statusBuildupPerHit: 10);
            Hunter = new Sample_Hunter("ハンター", maxHp: 150, maxStamina: 100, weapon: weapon);
            Head = new Sample_MonsterPart("頭", hitZonePercent: 65, durability: 200, brokenBonus: 10);
            Wing = new Sample_MonsterPart("翼", hitZonePercent: 30, durability: 150, brokenBonus: 15);
            Monster = new Sample_Monster("炎竜ヴァルロス", maxHp: 2000, rageThreshold: 300,
                new[] { Head, Wing });

            SlashUp = new Sample_AttackMove("斬り上げ", motionValuePercent: 40, staminaCost: 15);
            ChargedSlash = new Sample_AttackMove("溜め斬り", motionValuePercent: 95, staminaCost: 85);
            TailSwipe = new Sample_AttackMove("尻尾回転", motionValuePercent: 60, staminaCost: 0, baseAttack: 80);

            // ★パターン: EntityRegistry の明示ID登録。
            // 登録順を変えても保存済みリプレイ/セーブのIDがズレない（登録順依存の根絶）。
            // IDはマスタデータと揃えるのが実戦の推奨。
            Registry.RegisterWithId(Hunter, 1);
            Registry.RegisterWithId(Monster, 2);
            Registry.RegisterWithId(Head, 10);
            Registry.RegisterWithId(Wing, 11);
            Registry.RegisterWithId(SlashUp, 100);
            Registry.RegisterWithId(ChargedSlash, 101);
            Registry.RegisterWithId(TailSwipe, 102);

            // --- 合成ルート: このクエストに存在する仕様だけを登録する（未登録＝オミット） ---
            new Sample_SharpnessHandler(Hunter, 1050).RegisterTo(Ctx.Hub);
            new Sample_AttackBoostHandler(Hunter, 1100).RegisterTo(Ctx.Hub);
            new Sample_WeaknessExploitHandler(Hunter).RegisterTo(Ctx.Hub);
            new Sample_GuardPerformanceHandler(Hunter, level: 2).RegisterTo(Ctx.Hub);
            new Sample_DemonDrugHandler(Hunter).RegisterTo(Ctx.Hub);
            new Sample_MonsterRageHandler(Monster).RegisterTo(Ctx.Hub);

            Driver = new Sample_ActionDriver(Ctx, Hunter, Monster);
        }
    }
}
