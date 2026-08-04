using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.Hub.Unity
{
    /// <summary>
    /// 契約層の純C#ベクトルと UnityEngine.Vector3 の相互変換。
    ///
    /// 契約（Seed.Hub.Contracts）をエンジン非依存に保つ代償として生じる変換を、
    /// この1箇所に閉じ込めるためのアダプタ。Unity側の基盤はこのアセンブリを参照して使う。
    /// </summary>
    public static class HubVectorExtensions
    {
        /// <summary>契約ベクトルを UnityEngine.Vector3 へ変換する。</summary>
        public static Vector3 ToUnity(this HubVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        /// <summary>UnityEngine.Vector3 を契約ベクトルへ変換する。</summary>
        public static HubVector3 ToHub(this Vector3 value)
        {
            return new HubVector3(value.x, value.y, value.z);
        }
    }
}
