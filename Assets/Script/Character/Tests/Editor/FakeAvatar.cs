using System.Collections.Generic;
using UnityEngine;

namespace Seed.Character.Tests
{
    /// <summary>
    /// 呼び出し痕跡を記録するテスト用 Avatar（MonoBehaviour を使わないための道具）。
    /// </summary>
    public sealed class FakeAvatar : IAvatar
    {
        /// <summary>呼び出し痕跡（"SetActive(True)" など）。</summary>
        public readonly List<string> Calls = new List<string>();

        /// <summary>行動遷移の記録。</summary>
        public readonly List<(BehaviorKey From, BehaviorKey To)> Transitions =
            new List<(BehaviorKey, BehaviorKey)>();

        /// <summary>表示中か。</summary>
        public bool IsActive;

        /// <summary>最後に反映された位置。</summary>
        public Vector3 LastPosition;

        /// <summary>最後に反映された回転。</summary>
        public Quaternion LastRotation = Quaternion.identity;

        /// <summary>最後に反映された移動速度。</summary>
        public float LastSpeed;

        /// <summary>ApplyPose が呼ばれた回数。</summary>
        public int ApplyPoseCount;

        /// <summary>表示状態を記録する。</summary>
        public void SetActive(bool active)
        {
            IsActive = active;
            Calls.Add($"SetActive({active})");
        }

        /// <summary>姿勢を記録する。</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            LastPosition = position;
            LastRotation = rotation;
            ApplyPoseCount++;
        }

        /// <summary>遷移を記録する。</summary>
        public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
        {
            Transitions.Add((previous, next));
            Calls.Add($"OnBehaviorChanged({previous}->{next})");
        }

        /// <summary>移動速度を記録する。</summary>
        public void SetLocomotionSpeed(float normalizedSpeed)
        {
            LastSpeed = normalizedSpeed;
        }

        /// <summary>差し込まれたイベント受け口（テストから PostAvatarEvent を叩ける）。</summary>
        public IAvatarEventSink Sink { get; private set; }

        /// <summary>解放されたか。</summary>
        public bool Released { get; private set; }

        /// <summary>受け口を記録する。</summary>
        public void BindEventSink(IAvatarEventSink sink)
        {
            Sink = sink;
            Calls.Add($"BindEventSink({(sink != null ? "set" : "null")})");
        }

        /// <summary>解放を記録する。</summary>
        public void Release()
        {
            Released = true;
            Calls.Add("Release()");
        }

        /// <summary>最後の遷移先が期待どおりかを返す。</summary>
        public bool LastTransitionTo(BehaviorKey key)
        {
            return Transitions.Count > 0 && Transitions[Transitions.Count - 1].To.Equals(key);
        }
    }
}
