namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】アクション層 → 事象計算層への入力。
    /// ヒット成立の判断（コリジョン・有効フレーム・ガード姿勢）はアクション層で完了している。
    /// </summary>
    public readonly struct Sample_HitRequest
    {
        /// <summary>攻撃側。</summary>
        public readonly Sample_Unit Attacker;
        /// <summary>防御側。</summary>
        public readonly Sample_Unit Defender;
        public readonly Sample_MonsterPart Part; // ハンター被弾時は null
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_AttackMove Move;
        /// <summary>ガード成立か（アクション層の判定結果）。</summary>
        public readonly bool WasGuarded;

        /// <summary>Sample_HitRequest を生成する。</summary>
        public Sample_HitRequest(Sample_Unit attacker, Sample_Unit defender, Sample_MonsterPart part,
            Sample_AttackMove move, bool wasGuarded)
        {
            Attacker = attacker;
            Defender = defender;
            Part = part;
            Move = move;
            WasGuarded = wasGuarded;
        }
    }
}
