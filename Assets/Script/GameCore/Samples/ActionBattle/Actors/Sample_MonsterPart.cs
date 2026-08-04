namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】モンスターの部位。肉質と部位耐久を持ち、破壊で軟化する。</summary>
    public sealed class Sample_MonsterPart
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>現在の肉質(%)。破壊で軟化する。</summary>
        public int HitZonePercent { get; private set; }
        /// <summary>部位耐久の残り。</summary>
        public int Durability { get; private set; }
        /// <summary>破壊済みか。</summary>
        public bool IsBroken { get; private set; }
        /// <summary>破壊時に肉質へ加算される軟化量。</summary>
        private readonly int _brokenBonus;

        /// <summary>Sample_MonsterPart を生成する。</summary>
        public Sample_MonsterPart(string name, int hitZonePercent, int durability, int brokenBonus)
        {
            Name = name;
            HitZonePercent = hitZonePercent;
            Durability = durability;
            _brokenBonus = brokenBonus;
        }

        /// <summary>部位蓄積（呼び出しはセクションのみの規約）。壊れたら true。</summary>
        public bool AccumulateDamage(int amount)
        {
            if (IsBroken)
            {
                return false;
            }
            Durability -= amount;
            if (Durability > 0)
            {
                return false;
            }
            Durability = 0;
            IsBroken = true;
            HitZonePercent += _brokenBonus;
            return true;
        }
    }
}
