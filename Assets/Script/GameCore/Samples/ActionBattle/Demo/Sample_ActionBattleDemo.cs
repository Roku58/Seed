namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】アクションバトル（モンハン風リアルタイム制）のデモ入口。
    ///
    /// 役割分担:
    /// - 世界の組み立て（登場物・ID登録・仕様の合成）… Sample_ActionWorld
    /// - 進行台本（どの入力をどの順で流すか）        … Sample_ActionBattleScenario
    /// - 入口とリプレイ検証                          … 本クラス
    ///
    /// 実演している基盤パターン:
    /// - EntityRegistry によるID方式レコード（直列化可能。参照方式は CommandBattle を参照）
    /// - InputJournal による入力記録と、VerifyReplay でのリプレイ再生＋StableHash 検証
    /// - 連鎖（爆破→部位破壊）・複数イベント購読（怒り）・状態レスハンドラー（鬼人薬）
    /// - ConditionMergePolicy（Extend/Overwrite）・SectionResult・EventScope
    /// </summary>
    public static class Sample_ActionBattleDemo
    {
        /// <summary>初撃のみ会心となり、展開が分かりやすいシード。</summary>
        public const uint DefaultSeed = 1041;

        /// <summary>デモを実行し、結果の詰まったコンテキストを返す（入力は InputJournal 拡張に記録される）。</summary>
        public static LogicContext RunDemo(uint seed = DefaultSeed, ICoreTraceListener trace = null)
        {
            var world = new Sample_ActionWorld(seed, trace);

            var journal = new InputJournal<Sample_ActionInput>(32)
            {
                Seed = seed,
                Version = 1,
            };
            world.Ctx.AddExtension(journal);

            Sample_ActionBattleScenario.Play(world, journal);

            return world.Ctx;
        }

        /// <summary>
        /// ★パターン: リプレイ検証。
        /// ジャーナルの入力列を新しい世界で再生し、レコード列の StableHash を突き合わせる。
        /// 基盤が決定的なので、一致しなければ「演出乱数の混入」等のバグを意味する。
        /// </summary>
        public static bool VerifyReplay(LogicContext original, out uint originalHash, out uint replayHash)
        {
            var journal = original.GetExtension<InputJournal<Sample_ActionInput>>();

            var replayWorld = new Sample_ActionWorld(journal.Seed, trace: null);
            for (var i = 0; i < journal.Count; i++)
            {
                var entry = journal[i];
                replayWorld.Driver.Execute(entry.Input);
            }

            originalHash = HashRecords(original);
            replayHash = HashRecords(replayWorld.Ctx);
            return originalHash == replayHash;
        }

        /// <summary>
        /// レコード列の安定ハッシュ（★パターン: IHashableRecord による構造化ハッシュ）。
        /// 表示文字列ではなくレコードのフィールドをハッシュ化するため、
        /// Presenter の文言を直しても検証は壊れない。desync 検出にも使える。
        /// </summary>
        public static uint HashRecords(LogicContext ctx)
        {
            var log = Sample_ActionContext.Log(ctx);

            var hash = StableHash.Create();
            for (var i = 0; i < log.Count; i++)
            {
                log[i].AddTo(ref hash);
            }
            return hash.Value;
        }
    }
}
