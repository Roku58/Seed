using System.IO;
using Seed.App;
using Seed.Hub.Contracts;
using UnityEditor;
using UnityEngine;

namespace Seed.Tools.Editor
{
    /// <summary>
    /// マスターデータのベイク工程（入力 → MasterMemory バイナリ）。
    ///
    /// 実行時はこのバイナリを読む（無ければコード直書きへフォールバック）ため、
    /// データを変えたらベイクし直すのが運用の1工程になる。
    /// StreamingAssets に置くのはビルドへそのまま同梱されるため。
    /// </summary>
    public static class MasterDataBake
    {
        /// <summary>カタログをバイナリへ焼いて StreamingAssets に置く。</summary>
        [MenuItem("Seed/Master Data Bake")]
        public static void Bake()
        {
            var catalog = Sample_MasterCatalog.Build(new CharacterId(1), new CharacterId(2));
            var binary = Sample_MasterBinary.Bake(catalog);

            var path = Sample_MasterBinary.BinaryPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, binary);
            AssetDatabase.Refresh();
            Debug.Log($"[Master] ベイク完了: {path}（{binary.Length:N0} bytes）");
        }

        /// <summary>ベイク済みバイナリを削除してコード直書きへ戻す。</summary>
        [MenuItem("Seed/Master Data Bake 削除")]
        public static void DeleteBaked()
        {
            var path = Sample_MasterBinary.BinaryPath;
            if (File.Exists(path))
            {
                File.Delete(path);
                var meta = path + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
                AssetDatabase.Refresh();
                Debug.Log("[Master] ベイク済みバイナリを削除（コード直書きカタログへ戻る）");
            }
        }
    }
}
