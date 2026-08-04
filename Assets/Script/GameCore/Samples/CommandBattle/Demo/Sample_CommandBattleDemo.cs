namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】コマンドバトル（ポケモン風ターン制）のデモ。
    ///
    /// 世界の組み立ては Sample_CommandWorld、ターンの進行は本クラスが担当し、
    /// 「開幕ターン → お試しターン（巻き戻し）→ まもるターン」の3フェーズに分割している。
    ///
    /// 実演している基盤パターン:
    /// - 割り込み（せいでんき）・連鎖（クラボのみ）・複数イベント反応（晴れ）
    /// - CoreSnapshot + アクタースナップショット + 購読スナップショットによる巻き戻し
    /// - 事象レコードは参照方式（ID方式は Sample_ActionBattleDemo を参照）
    ///
    /// 同じシードなら事象レコード列は完全に一致する（ゴールデンログテストの前提）。
    /// </summary>
    public static class Sample_CommandBattleDemo
    {
        /// <summary>初撃で追加効果（やけど）が発動するシード。</summary>
        public const uint DefaultSeed = 11;

        /// <summary>デモを実行し、結果の詰まったコンテキストを返す。</summary>
        public static LogicContext RunDemo(uint seed = DefaultSeed, ICoreTraceListener trace = null)
        {
            var world = new Sample_CommandWorld(seed, trace);

            PlayOpeningTurn(world);
            PlayTrialTurnAndRollback(world);
            PlayProtectTurn(world);

            return world.Ctx;
        }

        /// <summary>
        /// ターン1: 晴れ補正つきほのおのパンチ（やけど）→ せいでんき割り込み → クラボのみ連鎖。
        /// </summary>
        private static void PlayOpeningTurn(Sample_CommandWorld world)
        {
            world.Driver.RunTurn(
                new Sample_TurnAction(world.Charizard, world.Pikachu, world.FirePunch),
                new Sample_TurnAction(world.Pikachu, world.Charizard, world.Thunderbolt));
        }

        /// <summary>
        /// ★巻き戻しパターン:
        /// 「もし2ターン目にまもるを使わなかったら？」をお試し実行し、丸ごと無かったことにする。
        /// 乱数状態・時刻（CoreSnapshot）＋購読状態＋HP・コンディション・ターン数・レコード件数を
        /// 復元するため、最終結果には一切痕跡が残らない（お試し中に乱数を消費しても巻き戻る）。
        /// </summary>
        private static void PlayTrialTurnAndRollback(Sample_CommandWorld world)
        {
            var snapshot = Sample_BattleSnapshot.Capture(world.Ctx, world.Charizard, world.Pikachu);

            world.Driver.RunTurn(
                new Sample_TurnAction(world.Charizard, world.Pikachu, world.FirePunch),
                new Sample_TurnAction(world.Pikachu, world.Charizard, world.Thunderbolt));

            Sample_BattleSnapshot.Restore(world.Ctx, world.Charizard, world.Pikachu, in snapshot);
        }

        /// <summary>
        /// 本番のターン2: まもる（優先度+4で先行）→ ほのおのパンチは発動判定で失敗する。
        /// </summary>
        private static void PlayProtectTurn(Sample_CommandWorld world)
        {
            world.Driver.RunTurn(
                new Sample_TurnAction(world.Pikachu, world.Charizard, world.Protect),
                new Sample_TurnAction(world.Charizard, world.Pikachu, world.FirePunch));
        }
    }
}
