namespace Seed.Core
{
    /// <summary>
    /// プラットフォーム非依存の安定ハッシュ（FNV-1a 32bit）。
    ///
    /// [目的] 「同じシード・同じ入力なら同じ結果」を機械的に確かめるための道具。
    /// - ゴールデンログテスト: 代表シナリオのレコード列のハッシュを保存し、変化を検知
    /// - リプレイ検証: 再生結果のハッシュを記録時と比較
    ///
    /// string.GetHashCode() はランタイム・実行ごとに値が変わるため使わないこと。
    ///
    /// 使用例:
    /// <code>
    ///   var hash = StableHash.Create();
    ///   for (var i = 0; i &lt; log.Count; i++) hash.Add(Presenter.Format(log[i]));
    ///   Debug.Log(hash.Value);
    /// </code>
    /// </summary>
    public struct StableHash
    {
        /// <summary>FNV-1aの初期値。</summary>
        private const uint OffsetBasis = 2166136261u;
        /// <summary>FNV-1aの乗数。</summary>
        private const uint Prime = 16777619u;

        /// <summary>現在のハッシュ値。</summary>
        private uint _value;

        /// <summary>現在のハッシュ値。</summary>
        public uint Value => _value;

        /// <summary>初期値で新規作成する。</summary>
        public static StableHash Create()
        {
            var h = new StableHash();
            h._value = OffsetBasis;
            return h;
        }

        /// <summary>値を追加する。</summary>
        public void Add(uint value)
        {
            AddByte((byte)value);
            AddByte((byte)(value >> 8));
            AddByte((byte)(value >> 16));
            AddByte((byte)(value >> 24));
        }

        /// <summary>値を追加する。</summary>
        public void Add(int value)
        {
            Add((uint)value);
        }

        /// <summary>値を追加する。</summary>
        public void Add(long value)
        {
            Add((uint)value);
            Add((uint)(value >> 32));
        }

        /// <summary>値を追加する。</summary>
        public void Add(bool value)
        {
            AddByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>値を追加する。</summary>
        public void Add(string value)
        {
            if (value == null)
            {
                AddByte(0);
                return;
            }
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                AddByte((byte)c);
                AddByte((byte)(c >> 8));
            }
        }

        /// <summary>
        /// バイト列をそのまま畳み込む（リプレイの改竄・破損検知など、生バイト列の照合用）。
        /// 既存の Add(int) 等とは別名にしてあるのは、byte を渡したときに
        /// オーバーロード解決が変わって既存のハッシュ値が動くのを防ぐため。
        /// </summary>
        public void AddBytes(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new System.ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(count));
            }
            for (var i = 0; i < count; i++)
            {
                AddByte(buffer[offset + i]);
            }
        }

        /// <summary>1バイトをFNV-1aで畳み込む。</summary>
        private void AddByte(byte b)
        {
            _value = (_value ^ b) * Prime;
        }
    }
}
