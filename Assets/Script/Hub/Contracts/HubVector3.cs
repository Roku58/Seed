using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 契約層で座標を運ぶための純C#ベクトル（UnityEngine.Vector3 の代役）。
    ///
    /// 契約層をエンジン非依存（noEngineReferences）に保つために存在する。
    /// これにより「ヘッドレスでの再計算・検証」「エンジン外ツール」「純C#テスト」で
    /// 契約をそのまま再利用できる。Unity側との相互変換は Seed.Hub.Unity の
    /// HubVectorExtensions（ToUnity / ToHub）を使う。
    /// </summary>
    public readonly struct HubVector3 : IEquatable<HubVector3>
    {
        /// <summary>原点。</summary>
        public static readonly HubVector3 Zero = new HubVector3(0f, 0f, 0f);

        /// <summary>X成分。</summary>
        public readonly float X;

        /// <summary>Y成分。</summary>
        public readonly float Y;

        /// <summary>Z成分。</summary>
        public readonly float Z;

        /// <summary>HubVector3 を生成する。</summary>
        public HubVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>各成分が一致するか。</summary>
        public bool Equals(HubVector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        /// <summary>各成分が一致するか。</summary>
        public override bool Equals(object obj)
        {
            return obj is HubVector3 other && Equals(other);
        }

        /// <summary>成分から合成したハッシュ。</summary>
        public override int GetHashCode()
        {
            var hash = X.GetHashCode();
            hash = (hash * 397) ^ Y.GetHashCode();
            hash = (hash * 397) ^ Z.GetHashCode();
            return hash;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"({X:0.##}, {Y:0.##}, {Z:0.##})";
        }
    }
}
