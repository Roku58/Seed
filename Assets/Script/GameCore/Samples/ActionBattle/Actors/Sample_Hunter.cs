using System;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】ハンター。</summary>
    public sealed class Sample_Hunter : Sample_Unit
    {
        /// <summary>最大スタミナ。</summary>
        public int MaxStamina { get; }
        /// <summary>現在スタミナ。</summary>
        public int Stamina { get; private set; }
        /// <summary>装備武器。</summary>
        public Sample_WeaponData Weapon { get; }

        /// <summary>Sample_Hunter を生成する。</summary>
        public Sample_Hunter(string name, int maxHp, int maxStamina, Sample_WeaponData weapon) : base(name, maxHp)
        {
            MaxStamina = maxStamina;
            Stamina = maxStamina;
            Weapon = weapon;
        }

        /// <summary>スタミナ消費の派生先フック（既定では何もしない）。</summary>
        protected override void OnConsumeStamina(int amount) => Stamina = Math.Max(0, Stamina - amount);
        /// <summary>スタミナ回復の派生先フック（既定では何もしない）。</summary>
        protected override void OnRegenStamina(int amount) => Stamina = Math.Min(MaxStamina, Stamina + amount);
    }
}
