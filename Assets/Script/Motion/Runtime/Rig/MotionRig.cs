using System.Collections.Generic;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>リグ1本の契約（姿勢・IKの適用単位）。</summary>
    public interface IPoseRig
    {
        /// <summary>
        /// 適用率（0で見た目には無効。0の間に完全停止するか内部状態だけ進めるかは
        /// 各リグが決める。距離フェードなどは呼び出し側が書く）。
        /// </summary>
        float Weight { get; set; }

        /// <summary>ボーンへ適用する（LateUpdate タイミング＝アニメの上書き）。</summary>
        void Apply();
    }

    /// <summary>
    /// 姿勢・IKリグの統括（登録順に LateUpdate で適用する）。
    ///
    /// LateUpdate なのは Animator/Playables の書き込みが Update 後に確定するため——
    /// アニメが作った姿勢の**上に**、注視・IK を重ねるのが正しい順序。
    /// 規約「状態は Tick、艶は Update」——リグは見た目の補正であり、
    /// 真実（ActorPose・行動状態）には一切書き込まない。
    /// 適用順は登録順（姿勢→IK の順に登録するのが目安。後着ほど強い）。
    /// </summary>
    public sealed class MotionRig : MonoBehaviour
    {
        /// <summary>登録済みのリグ（登録順 = 適用順）。</summary>
        private readonly List<IPoseRig> _rigs = new List<IPoseRig>(4);

        /// <summary>リグを追加する（流れるように書ける糖衣）。</summary>
        public MotionRig With(IPoseRig rig)
        {
            if (rig != null)
            {
                _rigs.Add(rig);
            }
            return this;
        }

        /// <summary>アニメ確定後に全リグを適用する。</summary>
        private void LateUpdate()
        {
            for (var i = 0; i < _rigs.Count; i++)
            {
                // Weight=0 の解釈は各リグへ委ねる（IK系は何もしないのが正解、
                // 揺れものはシミュレーションだけ進めて再開時の連続性を保つのが正解のため）
                _rigs[i].Apply();
            }
        }
    }
}
