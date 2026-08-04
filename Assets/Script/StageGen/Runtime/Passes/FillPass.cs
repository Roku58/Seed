using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>全セルを指定種別で埋める（各ジェネレーターの下地）。</summary>
    public sealed class FillPass : IGenerationPass
    {
        /// <summary>埋める種別。</summary>
        private readonly CellType _cell;

        /// <summary>FillPass を生成する。</summary>
        public FillPass(CellType cell)
        {
            _cell = cell;
        }

        /// <summary>パス名。</summary>
        public string Name => "Fill";

        /// <summary>全セルを埋める。</summary>
        public void Execute(StageBlueprint blueprint, DeterministicRandom random)
        {
            blueprint.Grid.Fill(_cell);
        }
    }
}
