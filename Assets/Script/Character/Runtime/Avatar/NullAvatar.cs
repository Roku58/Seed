using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 何も表示しない Avatar（純C#）。
    /// 表示物がまだ無い段階の Actor や、ヘッドレスでの動作確認に使う。
    /// アニメーションイベントの発生源を持たないため、受け口は保持すらしない。
    /// </summary>
    public sealed class NullAvatar : IAvatar
    {
        /// <summary>共有インスタンス（状態を持たないため使い回せる）。</summary>
        public static readonly NullAvatar Instance = new NullAvatar();

        /// <summary>何もしない。</summary>
        public void SetActive(bool active)
        {
        }

        /// <summary>何もしない。</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
        }

        /// <summary>何もしない。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
        }

        /// <summary>何もしない。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
        }

        /// <summary>何もしない（イベントの発生源が無い）。</summary>
        public void BindEventSink(IAvatarEventSink sink)
        {
        }

        /// <summary>何もしない（解放する実体が無い）。</summary>
        public void Release()
        {
        }
    }
}
