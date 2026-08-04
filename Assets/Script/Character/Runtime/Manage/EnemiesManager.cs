using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// エネミー陣営の管理（FactionId.Enemies）。
    /// 現状は基底そのままだが、「敵だけの共通処理」（ヘイト管理・湧き制御など）の
    /// 置き場として型を分けている。
    /// 第3勢力（中立NPC等）が必要なら、UnitManager を継承した陣営クラスを
    /// アプリ側で追加し CharactersManager.AddManager するだけでよい。
    /// </summary>
    public sealed class EnemiesManager : UnitManager
    {
        /// <summary>EnemiesManager を生成する。</summary>
        public EnemiesManager(CharacterRegistry registry)
            : base(registry, FactionId.Enemies)
        {
        }
    }
}
