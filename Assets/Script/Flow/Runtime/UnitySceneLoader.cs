using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seed.Flow
{
    /// <summary>
    /// SceneManager ベースの ISceneLoader 実装（本番用）。
    /// AsyncOperation を IFlowOperation へ包み、GameFlow のポーリングに乗せる。
    /// </summary>
    public sealed class UnitySceneLoader : ISceneLoader
    {
        /// <summary>AsyncOperation の進捗アダプタ。</summary>
        private sealed class Handle : IFlowOperation
        {
            /// <summary>包んでいる作業（null なら完了扱い）。</summary>
            private readonly AsyncOperation _operation;

            /// <summary>Handle を生成する。</summary>
            public Handle(AsyncOperation operation)
            {
                _operation = operation;
            }

            /// <summary>完了したか。</summary>
            public bool IsDone => _operation == null || _operation.isDone;

            /// <summary>進捗（0〜1）。</summary>
            public float Progress => _operation == null ? 1f : _operation.progress;
        }

        /// <summary>シーンを非同期ロードする。</summary>
        public IFlowOperation LoadScene(string sceneName, bool additive = false)
        {
            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            return new Handle(SceneManager.LoadSceneAsync(sceneName, mode));
        }

        /// <summary>シーンを非同期アンロードする。</summary>
        public IFlowOperation UnloadScene(string sceneName)
        {
            return new Handle(SceneManager.UnloadSceneAsync(sceneName));
        }
    }
}
