using System;
using System.IO;

namespace Seed.Core
{
    /// <summary>
    /// InputJournal のバイト列への保存/復元（リプレイファイルの中身）。
    ///
    /// フォーマット:
    ///   magic("SIMJ") / フォーマット版数 / ロジック版数 / シード / 件数 /
    ///   (時刻ms + 入力)×件数 / ペイロードのハッシュ(FNV-1a 32bit)
    ///
    /// - ロジック版数（journal.Version）が異なるリプレイは FromBytes が null＝再生拒否が既定動作
    /// - フォーマット版数は「この直列化形式そのものの版」で、ロジック版数とは別物。
    ///   末尾ハッシュを足した今回の変更で 1 → 2 へ上げた（旧形式のファイルは
    ///   フォーマット版数の不一致、または仮に値が一致してもハッシュ照合で必ず null になる
    ///   ＝旧ファイルを誤って読み進める可能性はない）
    /// - ファイルI/Oや圧縮はゲーム側の責務（ここでは byte[] までを担当）
    ///
    /// [契約: 不正入力は必ず null]
    /// 外部ファイルは「壊れている・改竄されている」前提で扱う。
    /// 件数の負値・過大値（＝巨大確保）・途中切れ・ハッシュ不一致は
    /// すべて null を返し、例外を呼び出し側へ漏らさない。
    /// </summary>
    public static class InputJournalCodec
    {
        private const int Magic = 0x4A4D4953; // "SIMJ"

        /// <summary>この直列化形式の版数（末尾ハッシュの追加で 1 → 2）。</summary>
        private const int FormatVersion = 2;

        /// <summary>magic＋フォーマット版数＋ロジック版数＋シード＋件数のバイト数。</summary>
        private const int HeaderSize = 4 + 4 + 4 + 4 + 4;

        /// <summary>末尾ハッシュ（フッタ）のバイト数。</summary>
        private const int FooterSize = 4;

        /// <summary>1件あたりの最小バイト数（時刻ms の分。入力の分はコーデック次第で増える）。</summary>
        private const int MinEntrySize = 8;

        /// <summary>ジャーナルをバイト列へ変換する（magic・版数・シード・末尾ハッシュ込み）。</summary>
        public static byte[] ToBytes<TInput>(InputJournal<TInput> journal, IInputCodec<TInput> codec)
            where TInput : struct
        {
            if (journal == null)
            {
                throw new ArgumentNullException(nameof(journal));
            }
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            using (var stream = new MemoryStream(64 + journal.Count * 16))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(journal.Version);
                writer.Write(journal.Seed);
                writer.Write(journal.Count);
                for (var i = 0; i < journal.Count; i++)
                {
                    var entry = journal[i];
                    writer.Write(entry.TimeMs);
                    codec.Write(writer, entry.Input);
                }
                writer.Flush();

                var payload = stream.ToArray();
                var result = new byte[payload.Length + FooterSize];
                Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
                WriteUInt32(result, payload.Length, ComputeHash(payload, payload.Length));
                return result;
            }
        }

        /// <summary>
        /// 復元。null・長さ不足・magic不一致・フォーマット版数不一致・
        /// ロジック版数不一致（expectedVersion指定時）・件数異常・途中切れ・
        /// ハッシュ不一致は、すべて null。
        /// </summary>
        public static InputJournal<TInput> FromBytes<TInput>(byte[] bytes, IInputCodec<TInput> codec,
            int expectedVersion = -1) where TInput : struct
        {
            if (bytes == null || codec == null || bytes.Length < HeaderSize + FooterSize)
            {
                return null;
            }

            var payloadLength = bytes.Length - FooterSize;

            // 先にハッシュを照合する（1バイトでも改竄・破損していれば、解析そのものを始めない）
            if (ComputeHash(bytes, payloadLength) != ReadUInt32(bytes, payloadLength))
            {
                return null;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, 0, payloadLength, writable: false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Magic)
                    {
                        return null;
                    }
                    if (reader.ReadInt32() != FormatVersion)
                    {
                        return null;
                    }

                    var version = reader.ReadInt32();
                    if (expectedVersion >= 0 && version != expectedVersion)
                    {
                        return null;
                    }

                    var seed = reader.ReadUInt32();
                    var count = reader.ReadInt32();

                    // 件数の妥当性: 負値を弾き、残りバイト数から見て入りきらない件数も弾く。
                    // 検証せずに new InputJournal(count) すると、壊れた1整数で巨大確保が起きる。
                    var remaining = payloadLength - HeaderSize;
                    if (count < 0 || (long)count * MinEntrySize > remaining)
                    {
                        return null;
                    }

                    var journal = new InputJournal<TInput>(count) { Version = version, Seed = seed };
                    for (var i = 0; i < count; i++)
                    {
                        var timeMs = reader.ReadInt64();
                        var input = codec.Read(reader);
                        journal.Record(timeMs, in input);
                    }
                    return journal;
                }
            }
            catch (EndOfStreamException)
            {
                return null; // 途中切れ
            }
            catch (IOException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null; // 時刻の逆行など、中身が壊れている場合（InputJournal.Record が弾く）
            }
        }

        /// <summary>
        /// ペイロードの安定ハッシュ（FNV-1a 32bit）。
        /// StableHash をそのまま流用する＝ゴールデンログ・リプレイ検証と同じ畳み込みで、
        /// プラットフォーム・実行ごとに値が変わらないことが保証されている。
        /// </summary>
        private static uint ComputeHash(byte[] buffer, int length)
        {
            var hash = StableHash.Create();
            hash.Add(length); // 長さも混ぜる（末尾のゼロ埋め等で同一ハッシュにならないように）
            hash.AddBytes(buffer, 0, length);
            return hash.Value;
        }

        /// <summary>32bit値をリトルエンディアンで書く（BinaryWriter と同じ並び）。</summary>
        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>32bit値をリトルエンディアンで読む。</summary>
        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return buffer[offset]
                | ((uint)buffer[offset + 1] << 8)
                | ((uint)buffer[offset + 2] << 16)
                | ((uint)buffer[offset + 3] << 24);
        }
    }
}
