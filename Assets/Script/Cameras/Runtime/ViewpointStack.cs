using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.Cameras
{
    /// <summary>
    /// 視点の重ね合わせ台（純C#。どの視点が今有効かを決める唯一の判断者）。
    ///
    /// [2層構造にした理由]
    /// カメラの切り替えには性質の違う2種類がある——プレイヤーが選ぶ「常用の視点」
    /// （FPS/TPS の切替）と、イベントが一時的に奪う「演出の視点」（決着の寄り・照準）。
    /// 同じ仕組みで扱うと、演出が終わったときに「元がどっちだったか」を呼び出し側が
    /// 覚えておく必要が出る。基本（Base）と重ね（Overlay）に分けておけば、
    /// 演出は積んで外すだけで元の視点へ必ず戻る。
    ///
    /// [順不同で外せる] 重ねは「最後に積んだものが有効」だが、取り下げは順不同にできる。
    /// 演出が入れ違って終わっても（照準中に決着した等）破綻しないため。
    ///
    /// 実際のカメラ（Cinemachine）には触らない純C#——切替の判断だけを担うので
    /// EditMode で全パターンを検証できる。反映は <see cref="CameraDirector"/> の仕事。
    /// </summary>
    public sealed class ViewpointStack
    {
        /// <summary>重ねられた視点（末尾が最前面）。</summary>
        private readonly List<ViewpointId> _overlays = new List<ViewpointId>(4);

        /// <summary>基本の視点（常用カメラ）。</summary>
        public ViewpointId Base { get; private set; } = ViewpointId.None;

        /// <summary>今有効な視点（重ねがあれば最前面、無ければ基本）。</summary>
        public ViewpointId Active =>
            _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : Base;

        /// <summary>重ねの枚数。</summary>
        public int OverlayCount => _overlays.Count;

        /// <summary>基本の視点を差し替える。戻り値は有効な視点が変わったか。</summary>
        public bool SetBase(ViewpointId viewpoint)
        {
            var before = Active;
            Base = viewpoint;
            return !Active.Equals(before);
        }

        /// <summary>
        /// 視点を最前面へ重ねる。既に積まれていれば最前面へ移す（二重に積まない）。
        /// 戻り値は有効な視点が変わったか。
        /// </summary>
        public bool Push(ViewpointId viewpoint)
        {
            var before = Active;
            _overlays.Remove(viewpoint);
            _overlays.Add(viewpoint);
            return !Active.Equals(before);
        }

        /// <summary>
        /// 指定の視点を取り下げる（順不同）。None を渡すと最前面を1枚取り下げる。
        /// 戻り値は有効な視点が変わったか。
        /// </summary>
        public bool Pop(ViewpointId viewpoint)
        {
            if (_overlays.Count == 0)
            {
                return false;
            }
            var before = Active;
            if (viewpoint.Equals(ViewpointId.None))
            {
                _overlays.RemoveAt(_overlays.Count - 1);
            }
            else if (!_overlays.Remove(viewpoint))
            {
                return false; // 積まれていないものの取り下げは無視（二重終了に強くする）
            }
            return !Active.Equals(before);
        }

        /// <summary>重ねを全部外して基本へ戻す（フェーズ退場・強制解除用）。</summary>
        public bool ClearOverlays()
        {
            if (_overlays.Count == 0)
            {
                return false;
            }
            var before = Active;
            _overlays.Clear();
            return !Active.Equals(before);
        }

        /// <summary>指定の視点が重ねられているか。</summary>
        public bool Contains(ViewpointId viewpoint)
        {
            return _overlays.Contains(viewpoint);
        }
    }
}
