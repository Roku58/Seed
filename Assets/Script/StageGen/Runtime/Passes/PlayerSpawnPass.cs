using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// プレイヤーの開始地点を置く（走査順で最初の歩行可能セル＝決定的）。
    /// 敵の距離帯配置・出口の最遠点配置・バイオームの距離塗りは、この点を起点に計算する。
    /// </summary>
    public sealed class PlayerSpawnPass : IGenerationPass
    {
        /// <summary>パス名。</summary>
        public string Name => "PlayerSpawn";

        /// <summary>開始地点を配置する。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            if (blueprint.Grid.TryFindFirstWalkable(out var x, out var y))
            {
                blueprint.AddPlacement(PlacementKind.PlayerSpawn, 0, x, y);
            }
        }
    }
}
