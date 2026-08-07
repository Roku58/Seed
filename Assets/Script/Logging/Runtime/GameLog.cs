using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Seed.Logging
{
    /// <summary>
    /// ZLogger のログを UnityEngine.Debug へ流す出口（Unity 向けプロセッサ）。
    ///
    /// ZLogger 本体は Console / File 向けの出口しか持たないため、Unity の
    /// Console ウィンドウに出すにはこの薄い橋が要る。重要度に応じて
    /// Log / LogWarning / LogError へ振り分けるので、既存の見え方と揃う。
    /// </summary>
    public sealed class UnityDebugLogProcessor : IAsyncLogProcessor
    {
        /// <summary>1件のログを Unity の Console へ書く。</summary>
        public void Post(IZLoggerEntry log)
        {
            try
            {
                var message = log.ToString();
                switch (log.LogInfo.LogLevel)
                {
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(message);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        UnityEngine.Debug.LogError(message);
                        break;
                    default:
                        UnityEngine.Debug.Log(message);
                        break;
                }
            }
            finally
            {
                log.Return(); // エントリはプール管理（返却漏れはリークになる）
            }
        }

        /// <summary>後始末（保留はない）。</summary>
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }

    /// <summary>
    /// ログ基盤の窓口（ZLogger のゼロアロケーション構造化ログを全基盤から使えるようにする）。
    ///
    /// [なぜ Debug.Log 直呼びから移すか]
    /// - 文字列補間のボックス化・連結が毎フレーム積もる（ZLogger は `ZLogInformation($"...")`
    ///   の補間をゼロアロケーションで処理する）
    /// - カテゴリ（どの基盤のログか）とログレベルで絞り込めず、量が増えると読めなくなる
    /// - 本番ビルドでファイル出力・外部送信へ切り替える差し込み口が無い
    ///
    /// [使い方] 合成ルートで一度 <see cref="InitializeForUnity"/>（または独自構成の
    /// <see cref="Initialize"/>）を呼び、各所は <see cref="CreateLogger(string)"/> で
    /// カテゴリ付きロガーを作って使う。未初期化でも安全側（Unity 出力）で動く。
    /// </summary>
    public static class GameLog
    {
        /// <summary>ロガーの供給元（差し替え可能）。</summary>
        private static ILoggerFactory _factory;

        /// <summary>初期化済みか。</summary>
        public static bool IsInitialized => _factory != null;

        /// <summary>Unity の Console へ出す標準構成で初期化する（合成ルートで一度だけ）。</summary>
        public static void InitializeForUnity(LogLevel minimumLevel = LogLevel.Debug)
        {
            Initialize(LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(minimumLevel);
                builder.AddZLoggerLogProcessor(new UnityDebugLogProcessor());
            }));
        }

        /// <summary>独自構成（ファイル出力・外部送信など）で初期化する。</summary>
        public static void Initialize(ILoggerFactory factory)
        {
            Shutdown();
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>カテゴリ付きロガーを作る（カテゴリ＝基盤名が読みやすい）。</summary>
        public static ILogger CreateLogger(string category)
        {
            return Factory().CreateLogger(category);
        }

        /// <summary>型名をカテゴリにしたロガーを作る。</summary>
        public static ILogger<T> CreateLogger<T>()
        {
            return Factory().CreateLogger<T>();
        }

        /// <summary>ログ基盤を畳む（アプリ終了時。バッファを吐き切る）。</summary>
        public static void Shutdown()
        {
            _factory?.Dispose();
            _factory = null;
        }

        /// <summary>供給元を返す（未初期化なら Unity 標準構成を遅延構築＝安全側）。</summary>
        private static ILoggerFactory Factory()
        {
            if (_factory == null)
            {
                InitializeForUnity();
            }
            return _factory;
        }
    }
}
