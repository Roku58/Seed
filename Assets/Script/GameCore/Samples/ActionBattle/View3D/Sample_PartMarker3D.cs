// ============================================================================
// 【サンプルコード】Sample_PartMarker3D
// 3D当たり判定で「どの部位に当たったか」を特定するためのマーカー。
// 部位コライダーのGameObjectに付け、対応するロジック側の部位を差し込む。
// アクション層（物理）とロジック層（Sample_MonsterPart）を結ぶ糊。
// ============================================================================

using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】コライダーとロジック部位の対応づけマーカー。</summary>
    public sealed class Sample_PartMarker3D : MonoBehaviour
    {
        /// <summary>このコライダーが表すロジック側の部位。</summary>
        public Sample_MonsterPart Part { get; private set; }

        /// <summary>対応する部位を設定する（生成直後に一度だけ呼ぶ）。</summary>
        public void Initialize(Sample_MonsterPart part)
        {
            Part = part;
        }
    }
}
