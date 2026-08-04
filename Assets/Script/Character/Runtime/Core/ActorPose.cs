using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 位置・向きの「表示用の写し」。
    /// 真実（ゲームルール上の座標があるなら）はロジック側にあり、ここは演出座標。
    /// Behavior が毎Tick書き込み、Actor が Tick の最後に Avatar（Transform）へ写す。
    /// 純C#なので移動・旋回の計算が MonoBehaviour なしでテストできる。
    /// </summary>
    public sealed class ActorPose
    {
        /// <summary>ワールド位置。</summary>
        public Vector3 Position { get; set; }

        /// <summary>ワールド回転。</summary>
        public Quaternion Rotation { get; set; } = Quaternion.identity;

        /// <summary>今Tickの水平速度（m/s）。Avatarの歩行ブレンド用。</summary>
        public float PlanarSpeed { get; set; }

        /// <summary>指定方向へ滑らかに向き直る（水平のみ）。</summary>
        public void FaceTowards(Vector3 worldDirection, float turnSpeedDegPerSec, float deltaTime)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }
            var target = Quaternion.LookRotation(worldDirection.normalized);
            Rotation = Quaternion.RotateTowards(Rotation, target, turnSpeedDegPerSec * deltaTime);
        }

        /// <summary>他の姿勢を丸ごと写す（Actor切替の状態引き継ぎ用）。</summary>
        public void CopyFrom(ActorPose other)
        {
            Position = other.Position;
            Rotation = other.Rotation;
            PlanarSpeed = other.PlanarSpeed;
        }
    }
}
