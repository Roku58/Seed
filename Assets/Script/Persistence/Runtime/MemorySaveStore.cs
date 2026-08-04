using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.Persistence
{
    /// <summary>
    /// メモリ上の ISaveStore 実装（テスト・体験版・保存禁止プラットフォーム用）。
    /// 封筒・検証を通らない素通し実装だが、契約の挙動（存在しないkeyはfalse等）は同じ。
    /// </summary>
    public sealed class MemorySaveStore : ISaveStore
    {
        /// <summary>key→データ。</summary>
        private readonly Dictionary<string, byte[]> _entries = new Dictionary<string, byte[]>();

        /// <summary>保存件数（テスト用）。</summary>
        public int Count => _entries.Count;

        /// <summary>保存する（コピーを保持し、呼び出し側の後書きに影響されない）。</summary>
        public bool TrySave(string key, byte[] data)
        {
            if (string.IsNullOrEmpty(key) || data == null)
            {
                return false;
            }
            _entries[key] = (byte[])data.Clone();
            return true;
        }

        /// <summary>読み込む（コピーを返す）。</summary>
        public bool TryLoad(string key, out byte[] data)
        {
            if (!string.IsNullOrEmpty(key) && _entries.TryGetValue(key, out var stored))
            {
                data = (byte[])stored.Clone();
                return true;
            }
            data = null;
            return false;
        }

        /// <summary>存在するか。</summary>
        public bool Exists(string key)
        {
            return !string.IsNullOrEmpty(key) && _entries.ContainsKey(key);
        }

        /// <summary>削除する。</summary>
        public bool Delete(string key)
        {
            return !string.IsNullOrEmpty(key) && _entries.Remove(key);
        }
    }
}
