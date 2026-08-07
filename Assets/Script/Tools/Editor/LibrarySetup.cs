using NugetForUnity;
using NugetForUnity.Models;
using UnityEditor;
using UnityEngine;

namespace Seed.Tools.Editor
{
    /// <summary>
    /// 外部ライブラリ（NuGet）のセットアップツール。
    ///
    /// [なぜ必要か] NuGetForUnity の自動復元は「packages.config に閉包が列挙済み」を
    /// 前提とした高速モード（依存解決なし）で走る。新しい環境の初回構築や CI では、
    /// ここから正規のインストール（依存解決＋Unity同梱ライブラリのスキップ）を実行する。
    ///
    /// 使い方:
    /// - エディタ: メニュー Seed/Setup/Install NuGet Packages
    /// - CI: -batchmode -executeMethod Seed.Tools.Editor.LibrarySetup.InstallFromCommandLine
    ///
    /// 採用ライブラリを増やすときは <see cref="Packages"/> に1行足す（バージョンは明示固定。
    /// 「最新を追う」と環境ごとに差が出て決定性が壊れるため）。
    /// </summary>
    public static class LibrarySetup
    {
        /// <summary>採用している NuGet パッケージの台帳（ID・固定バージョン）。</summary>
        private static readonly (string Id, string Version)[] Packages =
        {
            ("MessagePack", "3.1.8"),                    // シリアライザ（MasterMemory の土台）
            ("MasterMemory", "3.0.4"),                   // 読み取り専用インメモリDB（マスターデータ）
            ("Microsoft.Extensions.Logging", "8.0.1"),   // ログ抽象（ZLogger の土台）
            ("Microsoft.Extensions.Logging.Abstractions", "8.0.2"), // ILogger の定義元
            ("Microsoft.Extensions.DependencyInjection.Abstractions", "8.0.2"),
            ("Microsoft.Bcl.AsyncInterfaces", "8.0.0"),
            ("System.Diagnostics.DiagnosticSource", "8.0.1"),
            ("System.IO.Hashing", "8.0.0"),
            ("ZLogger", "2.5.10"),                       // ゼロアロケーション構造化ログ
        };

        /// <summary>メニューから実行する。</summary>
        [MenuItem("Seed/Setup/Install NuGet Packages")]
        public static void Install()
        {
            var failed = 0;
            foreach (var (id, version) in Packages)
            {
                var ok = NugetPackageInstaller.InstallIdentifier(
                    new NugetPackageIdentifier(id, version), refreshAssets: false);
                Debug.Log($"[LibrarySetup] {id} {version}: {(ok ? "OK" : "失敗")}");
                if (!ok)
                {
                    failed++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[LibrarySetup] 完了（{Packages.Length - failed}/{Packages.Length} 成功）");
        }

        /// <summary>CI・コマンドライン用（失敗時は非0で終了する）。</summary>
        public static void InstallFromCommandLine()
        {
            var failed = 0;
            foreach (var (id, version) in Packages)
            {
                if (!NugetPackageInstaller.InstallIdentifier(
                    new NugetPackageIdentifier(id, version), refreshAssets: false))
                {
                    failed++;
                }
            }
            AssetDatabase.Refresh();
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
