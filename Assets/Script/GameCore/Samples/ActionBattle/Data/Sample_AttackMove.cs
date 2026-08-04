namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】攻撃モーションデータ。モーション値(%)がダメージの主係数。</summary>
    public sealed class Sample_AttackMove
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>モーション値(%)。</summary>
        public int MotionValuePercent { get; }
        /// <summary>消費スタミナ。</summary>
        public int StaminaCost { get; }
        /// <summary>モンスター側モーションの基礎攻撃力（ハンターは武器値を使うので0）。</summary>
        public int BaseAttack { get; }

        /// <summary>Sample_AttackMove を生成する。</summary>
        public Sample_AttackMove(string name, int motionValuePercent, int staminaCost, int baseAttack = 0)
        {
            Name = name;
            MotionValuePercent = motionValuePercent;
            StaminaCost = staminaCost;
            BaseAttack = baseAttack;
        }
    }
}
