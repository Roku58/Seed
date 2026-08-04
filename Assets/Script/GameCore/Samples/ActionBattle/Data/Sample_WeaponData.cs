namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>【サンプル】武器データ（純データ。振る舞いはハンドラー側）。</summary>
    public sealed class Sample_WeaponData
    {
        /// <summary>表示・デバッグ用の名前。</summary>
        public string Name { get; }
        /// <summary>攻撃力。</summary>
        public int Attack { get; }
        /// <summary>武器の会心率(‰)。</summary>
        public int AffinityPermille { get; }
        /// <summary>状態異常種別。</summary>
        public Sample_StatusKind Status { get; }
        /// <summary>1ヒットあたりの状態異常蓄積量。</summary>
        public int StatusBuildupPerHit { get; }

        /// <summary>Sample_WeaponData を生成する。</summary>
        public Sample_WeaponData(string name, int attack, int affinityPermille,
            Sample_StatusKind status = Sample_StatusKind.None, int statusBuildupPerHit = 0)
        {
            Name = name;
            Attack = attack;
            AffinityPermille = affinityPermille;
            Status = status;
            StatusBuildupPerHit = statusBuildupPerHit;
        }
    }
}
