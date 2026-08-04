using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// Logic に見せる「自分の今」のスナップショット（Controller が毎Tick組み立てる）。
    /// 他キャラの位置など外界の情報は、Logic 実装のコンストラクタで
    /// ICharacterQuery を注入して読む（本型を太らせない）。
    /// </summary>
    public readonly struct LogicFrame
    {
        /// <summary>自分のID。</summary>
        public readonly CharacterId SelfId;

        /// <summary>自分の位置（表示用の写し）。</summary>
        public readonly Vector3 SelfPosition;

        /// <summary>自分の向き。</summary>
        public readonly Quaternion SelfRotation;

        /// <summary>生存しているか（表示用の写し）。</summary>
        public readonly bool IsAlive;

        /// <summary>今Tickの経過秒。</summary>
        public readonly float DeltaTime;

        /// <summary>LogicFrame を生成する。</summary>
        public LogicFrame(CharacterId selfId, Vector3 selfPosition, Quaternion selfRotation,
            bool isAlive, float deltaTime)
        {
            SelfId = selfId;
            SelfPosition = selfPosition;
            SelfRotation = selfRotation;
            IsAlive = isAlive;
            DeltaTime = deltaTime;
        }
    }
}
