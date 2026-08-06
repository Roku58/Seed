using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 地面の当たり情報（純C#の写し）。
    ///
    /// Unity の RaycastHit をそのまま基盤の外へ出さないのは、接地の計算を
    /// Physics 抜きでテストできるようにするため——階段・段差・傾斜の解き方は
    /// 数学であり、レイキャストの実装とは分けて検証できる。
    /// </summary>
    public readonly struct GroundHit
    {
        /// <summary>接地点（ワールド）。</summary>
        public readonly Vector3 Point;

        /// <summary>接地面の法線（ワールド・単位ベクトル）。</summary>
        public readonly Vector3 Normal;

        /// <summary>問い合わせ起点からの距離。</summary>
        public readonly float Distance;

        /// <summary>GroundHit を生成する。</summary>
        public GroundHit(Vector3 point, Vector3 normal, float distance)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
        }
    }

    /// <summary>
    /// 地面問い合わせの契約（足IKが「床はどこか」を知るための唯一の窓口）。
    ///
    /// 実装を差し替えられるようにしてあるのは2つの理由から:
    /// - EditMode テストで階段・傾斜・穴を偽の地面として与え、接地の解き方を検証できる
    /// - 物理コライダー以外（ハイトマップ・ボクセル・NavMesh）を地面にするゲームでも
    ///   本体を書き換えずに済む
    /// </summary>
    public interface IGroundProbe
    {
        /// <summary>
        /// 起点から向き方向へ地面を探す。見つかれば true。
        /// 実装は「最も近い1点」を返す規約（複数ヒットの選別は実装側の責務）。
        /// </summary>
        bool TryProbe(Vector3 origin, Vector3 direction, float maxDistance, out GroundHit hit);
    }

    /// <summary>
    /// Physics.Raycast による地面問い合わせ（本番用）。
    ///
    /// レイヤーマスクで「何を地面とみなすか」を決める。トリガーは既定で無視する——
    /// 当たり判定用のトリガー領域を踏んで足が浮くのを防ぐため。
    /// </summary>
    public sealed class PhysicsGroundProbe : IGroundProbe
    {
        /// <summary>地面とみなすレイヤー。</summary>
        private readonly LayerMask _groundMask;

        /// <summary>トリガーの扱い（既定は無視）。</summary>
        private readonly QueryTriggerInteraction _triggerInteraction;

        /// <summary>PhysicsGroundProbe を生成する。</summary>
        public PhysicsGroundProbe(LayerMask groundMask,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            _groundMask = groundMask;
            _triggerInteraction = triggerInteraction;
        }

        /// <summary>レイキャストで地面を探す。</summary>
        public bool TryProbe(Vector3 origin, Vector3 direction, float maxDistance, out GroundHit hit)
        {
            if (Physics.Raycast(origin, direction, out var raycastHit, maxDistance,
                _groundMask, _triggerInteraction))
            {
                hit = new GroundHit(raycastHit.point, raycastHit.normal, raycastHit.distance);
                return true;
            }
            hit = default;
            return false;
        }
    }
}
