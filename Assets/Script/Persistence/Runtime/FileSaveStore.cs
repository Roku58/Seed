using System;
using System.IO;
using Seed.Hub.Contracts;

namespace Seed.Persistence
{
    /// <summary>
    /// ファイルベースの ISaveStore 実装（本番用）。
    ///
    /// - 封筒形式（SaveEnvelope）: 破損・改変・途中切れを開封時に検知して false
    /// - 原子的書き込み: 一時ファイルへ書き切ってから置換する。
    ///   書き込み中にクラッシュしても、既存の保存データは無傷で残る
    /// - 保存先ディレクトリはコンストラクタ注入（Unityなら Application.persistentDataPath を渡す。
    ///   注入式なのでヘッドレスサーバ・テストでも同じ実装がそのまま動く）
    /// - key はファイル名になるため英数字と - _ . のみ許可（パス注入の防止）
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        /// <summary>保存ファイルの拡張子。</summary>
        private const string Extension = ".sav";

        /// <summary>封筒に書く形式版数。</summary>
        private readonly int _version;

        /// <summary>保存先ディレクトリ。</summary>
        private readonly string _directory;

        /// <summary>FileSaveStore を生成する（ディレクトリは無ければ作る）。</summary>
        public FileSaveStore(string directory, int version = 1)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("保存先ディレクトリが空", nameof(directory));
            }
            _directory = directory;
            _version = version;
            Directory.CreateDirectory(directory);
        }

        /// <summary>保存する（一時ファイル→置換の原子的書き込み）。失敗は false。</summary>
        public bool TrySave(string key, byte[] data)
        {
            if (!IsValidKey(key) || data == null)
            {
                return false;
            }
            try
            {
                var path = PathOf(key);
                var tempPath = path + ".tmp";
                File.WriteAllBytes(tempPath, SaveEnvelope.Wrap(_version, data));

                // 置換は原子的に。初回保存（本体が無い）は Move で十分
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>読み込む。存在しない・破損・版数不一致は false。</summary>
        public bool TryLoad(string key, out byte[] data)
        {
            data = null;
            if (!IsValidKey(key))
            {
                return false;
            }
            try
            {
                var path = PathOf(key);
                if (!File.Exists(path))
                {
                    return false;
                }
                if (!SaveEnvelope.TryUnwrap(File.ReadAllBytes(path), out var version, out var payload))
                {
                    return false; // 破損・改変・途中切れ
                }
                if (version != _version)
                {
                    return false; // 版数不一致（マイグレーションは上位層の判断）
                }
                data = payload;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>存在するか。</summary>
        public bool Exists(string key)
        {
            return IsValidKey(key) && File.Exists(PathOf(key));
        }

        /// <summary>削除する。</summary>
        public bool Delete(string key)
        {
            if (!IsValidKey(key))
            {
                return false;
            }
            try
            {
                var path = PathOf(key);
                if (!File.Exists(path))
                {
                    return false;
                }
                File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>key からファイルパスを組む。</summary>
        private string PathOf(string key)
        {
            return Path.Combine(_directory, key + Extension);
        }

        /// <summary>key の妥当性（英数字と - _ . のみ。パス区切りの注入を防ぐ）。</summary>
        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
