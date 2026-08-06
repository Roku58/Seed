using UnityEngine;

namespace Seed.World
{
    /// <summary>
    /// 原点回帰の判断役（純C#。いつ・どれだけ世界をずらすかを決めるだけ）。
    ///
    /// [なぜ必要か] 単精度浮動小数（float）は絶対値が大きくなるほど刻みが粗くなる。
    /// 原点から数キロ離れると位置の刻みがミリメートル単位を超え、静止しているのに
    /// 表示が震える・当たり判定や IK が不安定になる・カメラの追従がガタつく。
    /// 座標系を大きくするのではなく「世界を原点へ引き戻す」のが定石で、
    /// 見た目は変わらないまま精度だけが回復する。
    ///
    /// [格子への丸め] ずらす量を SnapSize の倍数へ丸められる。丸めておくと
    /// タイル状の地形やノイズ由来の模様が「同じ位相」で並び続けるため、
    /// シフトの瞬間に模様が飛ぶのを避けられる。
    ///
    /// 実際に Transform を動かすのは <see cref="OriginShiftSystem"/> の仕事で、
    /// ここは判断と累積量の記録だけ——EditMode で閾値・丸め・往復変換を検証できる。
    /// </summary>
    public sealed class OriginShifter
    {
        /// <summary>原点からこの距離（m）を超えたらずらす。</summary>
        public float Threshold { get; set; }

        /// <summary>ずらす量を丸める格子の大きさ（m。0 なら丸めない）。</summary>
        public float SnapSize { get; set; }

        /// <summary>高さ方向もずらすか（既定は false＝水平のみ）。</summary>
        public bool ShiftVertical { get; set; }

        /// <summary>累積のずらし量（見た目の座標 − これ ＝ 開始時からの絶対座標）。</summary>
        public Vector3 TotalOffset { get; private set; }

        /// <summary>これまでにずらした回数（デバッグ・統計用）。</summary>
        public int ShiftCount { get; private set; }

        /// <summary>OriginShifter を生成する。</summary>
        public OriginShifter(float threshold = 2000f, float snapSize = 0f, bool shiftVertical = false)
        {
            Threshold = threshold;
            SnapSize = snapSize;
            ShiftVertical = shiftVertical;
        }

        /// <summary>焦点（通常はプレイヤー）が閾値を超えて原点から離れたか。</summary>
        public bool ShouldShift(Vector3 focusPosition)
        {
            var measured = ShiftVertical
                ? focusPosition
                : new Vector3(focusPosition.x, 0f, focusPosition.z);
            return measured.sqrMagnitude >= Threshold * Threshold;
        }

        /// <summary>
        /// 焦点を原点へ戻すためのずらし量を計算する（丸め・軸制限を適用済み）。
        /// 実際に適用するかは呼び出し側の判断で、この関数は状態を変えない。
        /// </summary>
        public Vector3 ComputeDelta(Vector3 focusPosition)
        {
            var delta = -focusPosition;
            if (!ShiftVertical)
            {
                delta.y = 0f;
            }
            if (SnapSize > 0f)
            {
                delta = new Vector3(
                    Mathf.Round(delta.x / SnapSize) * SnapSize,
                    ShiftVertical ? Mathf.Round(delta.y / SnapSize) * SnapSize : 0f,
                    Mathf.Round(delta.z / SnapSize) * SnapSize);
            }
            return delta;
        }

        /// <summary>ずらしを確定して累積へ加える（適用は呼び出し側が行う）。</summary>
        public void Commit(Vector3 delta)
        {
            TotalOffset += delta;
            ShiftCount++;
        }

        /// <summary>
        /// 判断・計算・確定をまとめて行う。ずらすべきでない、または丸めの結果
        /// ずらし量が 0 になった場合は false（無駄なシフトを起こさない）。
        /// </summary>
        public bool TryShift(Vector3 focusPosition, out Vector3 delta)
        {
            delta = Vector3.zero;
            if (!ShouldShift(focusPosition))
            {
                return false;
            }
            delta = ComputeDelta(focusPosition);
            if (delta.sqrMagnitude <= 0f)
            {
                return false;
            }
            Commit(delta);
            return true;
        }

        /// <summary>見た目の座標を開始時からの絶対座標へ戻す（セーブ・ログ・遠距離判定用）。</summary>
        public Vector3 ToAbsolute(Vector3 shiftedPosition)
        {
            return shiftedPosition - TotalOffset;
        }

        /// <summary>絶対座標を今の見た目の座標へ変換する（セーブから復元するとき）。</summary>
        public Vector3 ToShifted(Vector3 absolutePosition)
        {
            return absolutePosition + TotalOffset;
        }
    }
}
