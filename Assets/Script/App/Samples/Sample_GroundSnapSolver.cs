using Seed.Character;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 地面へ吸着する移動解決器（App の方針実装。階段・坂を歩けるようにする）。
    ///
    /// Behavior が出す希望変位は水平だけなので、そのままでは段や坂を上れない。
    /// 本実装は水平移動を素通しした上で、移動先の**真下の地面**を探して高さを合わせる。
    /// 段差の上限（<paramref name="maxStepHeight"/>）を超える高さの変化は拒否して
    /// 直前の高さを保つ——これが「壁は登れないが階段は上れる」の分かれ目になる。
    ///
    /// [なぜ基盤ではなく App にあるか] 「段差をどこまで上れるか」「浮いたときに落とすか」は
    /// ゲームの手触りそのもので、作品ごとに違う。基盤（<see cref="IMotionSolver"/>）は
    /// 差し替え口だけを持ち、こうした方針は App 側に置く規約である。
    ///
    /// 足元の細かな凹凸（各段の高さ・傾斜）は本体を動かさず
    /// 足IK（Seed.Motion の FootIkRig）が吸収する。役割が二段になっている。
    /// </summary>
    public sealed class Sample_GroundSnapSolver : IMotionSolver
    {
        /// <summary>キャラ原点から足元までの距離（カプセルなら高さの半分）。</summary>
        private readonly float _footToOrigin;

        /// <summary>一度に上れる高さの上限（m）。</summary>
        private readonly float _maxStepHeight;

        /// <summary>地面とみなすレイヤー。</summary>
        private readonly int _groundMask;

        /// <summary>探索レイの上方への余裕（m）。</summary>
        private readonly float _probeUp;

        /// <summary>Sample_GroundSnapSolver を生成する。</summary>
        public Sample_GroundSnapSolver(float footToOrigin = 0.8f, float maxStepHeight = 0.35f,
            int groundMask = ~0, float probeUp = 1.2f)
        {
            _footToOrigin = footToOrigin;
            _maxStepHeight = maxStepHeight;
            _groundMask = groundMask;
            _probeUp = probeUp;
        }

        /// <summary>水平移動を適用し、高さを地面へ合わせる。</summary>
        public Vector3 Move(Vector3 currentPosition, Vector3 desiredDelta)
        {
            var next = currentPosition + new Vector3(desiredDelta.x, 0f, desiredDelta.z);
            var origin = next + Vector3.up * _probeUp;
            if (!Physics.Raycast(origin, Vector3.down, out var hit,
                _probeUp + _maxStepHeight + 4f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                return next; // 足場が見つからない（穴の上）ときは高さを変えない
            }
            var targetY = hit.point.y + _footToOrigin;
            if (targetY - currentPosition.y > _maxStepHeight)
            {
                next.y = currentPosition.y; // 高すぎる＝壁として扱い、登らない
                return next;
            }
            next.y = targetY;
            return next;
        }
    }
}
