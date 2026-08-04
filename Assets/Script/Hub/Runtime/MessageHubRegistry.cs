#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Seed.Hub
{
    /// <summary>
    /// 生きている MessageHub の台帳（開発時専用）。
    ///
    /// Hub はシングルトンではなく合成ルートが好きな数だけ生成するため、
    /// 外付けのトレーサ（Editorウィンドウ）が「今どのHubが存在するか」を知る術が
    /// 本来は無い——その一点だけを解決する観測用の弱参照リスト。
    /// リリースビルドではファイルごと消える（#if）。ゲームコードから参照してはならない。
    /// </summary>
    public static class MessageHubRegistry
    {
        /// <summary>生成された Hub（弱参照。GCされたものは列挙時に間引く）。</summary>
        private static readonly List<System.WeakReference<MessageHub>> Hubs =
            new List<System.WeakReference<MessageHub>>(4);

        /// <summary>Hub を台帳へ載せる（MessageHub のコンストラクタが呼ぶ）。</summary>
        public static void Register(MessageHub hub)
        {
            lock (Hubs)
            {
                Hubs.Add(new System.WeakReference<MessageHub>(hub));
            }
        }

        /// <summary>生きている Hub を buffer へ詰めて件数を返す（死んだ参照は間引く）。</summary>
        public static int CollectAlive(List<MessageHub> buffer)
        {
            buffer.Clear();
            lock (Hubs)
            {
                for (var i = Hubs.Count - 1; i >= 0; i--)
                {
                    if (Hubs[i].TryGetTarget(out var hub))
                    {
                        buffer.Add(hub);
                    }
                    else
                    {
                        Hubs.RemoveAt(i);
                    }
                }
            }
            buffer.Reverse(); // 生成順に戻す
            return buffer.Count;
        }
    }
}
#endif
