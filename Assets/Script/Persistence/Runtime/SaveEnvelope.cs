using System;

namespace Seed.Persistence
{
    /// <summary>
    /// 保存データの封筒（magic / 版数 / ペイロード長 / 本文 / ハッシュ）。
    ///
    /// ディスク上のデータは「外部から来るデータ」——半端に書かれた・壊れた・
    /// 改変されたバイト列を読む前提で、開封時に全検証を行い、
    /// 不正なら例外ではなく false で拒否する（InputJournalCodec と同じ契約）。
    /// </summary>
    public static class SaveEnvelope
    {
        /// <summary>ファイル先頭の識別子（"SEDS" = Seed Save）。</summary>
        private const int Magic = 0x53454453;

        /// <summary>ヘッダ長（magic + 版数 + ペイロード長）。</summary>
        private const int HeaderSize = 12;

        /// <summary>フッタ長（FNV-1aハッシュ）。</summary>
        private const int FooterSize = 4;

        /// <summary>ペイロードを封筒に包む。</summary>
        public static byte[] Wrap(int version, byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var bytes = new byte[HeaderSize + payload.Length + FooterSize];
            WriteInt(bytes, 0, Magic);
            WriteInt(bytes, 4, version);
            WriteInt(bytes, 8, payload.Length);
            Array.Copy(payload, 0, bytes, HeaderSize, payload.Length);
            WriteInt(bytes, HeaderSize + payload.Length, ComputeHash(bytes, 0, HeaderSize + payload.Length));
            return bytes;
        }

        /// <summary>封筒を開ける。破損・改変・途中切れは false。</summary>
        public static bool TryUnwrap(byte[] bytes, out int version, out byte[] payload)
        {
            version = 0;
            payload = null;
            if (bytes == null || bytes.Length < HeaderSize + FooterSize)
            {
                return false;
            }
            if (ReadInt(bytes, 0) != Magic)
            {
                return false;
            }
            var length = ReadInt(bytes, 8);
            if (length < 0 || bytes.Length != HeaderSize + length + FooterSize)
            {
                return false;
            }
            var expected = ReadInt(bytes, HeaderSize + length);
            if (ComputeHash(bytes, 0, HeaderSize + length) != expected)
            {
                return false; // 破損・改変
            }

            version = ReadInt(bytes, 4);
            payload = new byte[length];
            Array.Copy(bytes, HeaderSize, payload, 0, length);
            return true;
        }

        /// <summary>FNV-1a（32bit）。暗号強度は不要——破損検知が目的。</summary>
        private static int ComputeHash(byte[] bytes, int offset, int count)
        {
            unchecked
            {
                var hash = (uint)2166136261;
                for (var i = offset; i < offset + count; i++)
                {
                    hash = (hash ^ bytes[i]) * 16777619;
                }
                return (int)hash;
            }
        }

        /// <summary>リトルエンディアンで int を書く。</summary>
        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>リトルエンディアンで int を読む。</summary>
        private static int ReadInt(byte[] bytes, int offset)
        {
            return bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24);
        }
    }
}
