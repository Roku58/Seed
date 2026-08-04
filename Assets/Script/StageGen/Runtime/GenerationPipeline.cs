using System;
using System.Collections.Generic;
using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 生成パイプライン（パスの列を登録順に実行して設計図を作る）。
    ///
    /// 決定性の要:
    /// - 同じ seed・設定・パス列 → バイト単位で同一の設計図
    /// - **パスごとに親乱数から Fork した独立ストリームを渡す**——
    ///   パスを追加・削除しても他のパスの乱数列がズレないため、
    ///   パイプラインを拡張しても既存ステージのシード互換が壊れにくい
    /// </summary>
    public sealed class GenerationPipeline
    {
        /// <summary>パス列（登録順 = 実行順）。</summary>
        private readonly List<IGenerationPass> _passes = new List<IGenerationPass>(8);

        /// <summary>パスを追加する（流れるように書ける糖衣）。</summary>
        public GenerationPipeline Add(IGenerationPass pass)
        {
            if (pass == null)
            {
                throw new ArgumentNullException(nameof(pass));
            }
            _passes.Add(pass);
            return this;
        }

        /// <summary>設計図を生成する（純C#・決定的）。</summary>
        public StageBlueprint Generate(int width, int height, float cellSize, uint seed)
        {
            var blueprint = new StageBlueprint(width, height, cellSize, seed);
            var parent = new DeterministicRandom(seed);
            for (var i = 0; i < _passes.Count; i++)
            {
                // パスごとの独立ストリーム（順番に Fork するので列自体も決定的）
                _passes[i].Execute(blueprint, parent.Fork());
            }
            return blueprint;
        }
    }
}
