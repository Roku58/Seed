using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Seed.Assets
{
    /// <summary>
    /// Addressables による本番用ローダー。
    ///
    /// Addressables は「ハンドル」を返し、解放もハンドル単位で行う参照カウント式。
    /// 消費側にハンドルを持ち回らせると契約が Addressables に汚染されるため、
    /// 本クラスがアセット→ハンドルの対応を覚えておき、<see cref="Release"/> は
    /// アセットを渡すだけで済むようにしている。
    ///
    /// 前提: Addressables の設定（グループとアドレス）が済んでいること。
    /// アドレス付与の運用は Smart Addresser のルールで自動化する（ガイド18章）。
    /// </summary>
    public sealed class AddressablesAssetLoader : IAssetLoader
    {
        /// <summary>ロード済みアセット → ハンドルの束（同一アセットの多重ロードに対応）。</summary>
        private readonly Dictionary<Object, Stack<AsyncOperationHandle>> _handles =
            new Dictionary<Object, Stack<AsyncOperationHandle>>();

        /// <summary>アセットを読み込む（await 可能。破棄時は CancellationToken で中断）。</summary>
        public async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellation = default)
            where T : Object
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            var asset = await handle.ToUniTask(cancellationToken: cancellation);
            if (!_handles.TryGetValue(asset, out var stack))
            {
                stack = new Stack<AsyncOperationHandle>();
                _handles[asset] = stack;
            }
            stack.Push(handle);
            return asset;
        }

        /// <summary>プレハブを読み込んで実体化する（解放は ReleaseInstance）。</summary>
        public async UniTask<GameObject> InstantiateAsync(string key, Transform parent = null,
            CancellationToken cancellation = default)
        {
            var handle = Addressables.InstantiateAsync(key, parent);
            return await handle.ToUniTask(cancellationToken: cancellation);
        }

        /// <summary>アセットを返却する（対応するハンドルを1つ解放）。</summary>
        public void Release(Object asset)
        {
            if (asset == null || !_handles.TryGetValue(asset, out var stack) || stack.Count == 0)
            {
                return; // このローダー経由でないものは黙って無視しない方が良いが、二重解放事故の方が実害が大きい
            }
            Addressables.Release(stack.Pop());
            if (stack.Count == 0)
            {
                _handles.Remove(asset);
            }
        }

        /// <summary>実体を破棄・返却する。</summary>
        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
            }
        }
    }
}
