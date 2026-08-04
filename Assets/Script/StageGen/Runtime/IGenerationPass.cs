using Seed.Core;

namespace Seed.StageGen
{
    /// <summary>
    /// 生成パス1つ（設計図への上書き工程。「迷路を掘る」「バイオームを塗る」「敵を置く」…）。
    ///
    /// 迷路も街も洞窟も「同じ設計図へのパスの組み合わせ」でしかない——
    /// 地形の種類を増やす＝パスを1つ書く（Behavior / Consideration と同じ
    /// 「数だけ用意する」拡張点）。パスは渡された DeterministicRandom だけを乱数源にすること
    /// （UnityEngine.Random 等を混ぜると決定性が壊れる）。
    /// </summary>
    public interface IGenerationPass
    {
        /// <summary>デバッグ・ログ用のパス名。</summary>
        string Name { get; }

        /// <summary>設計図へ1工程ぶんの変更を加える。</summary>
        void Execute(StageBlueprint blueprint, DeterministicRandom random);
    }
}
