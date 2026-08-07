using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seed.Assets
{
    /// <summary>
    /// アセットロードの契約（消費側は「キーで頼んで await する」ことしか知らない）。
    ///
    /// [なぜ抽象化するか] ロードの実体（Addressables・Resources・アセットバンドル直・
    /// エディタ専用の AssetDatabase）はプロジェクトの時期や配信方針で変わる。
    /// 消費側をこの契約に留めておけば、実体の差し替えが合成ルートの1行で済み、
    /// テストでは偽実装を注入できる。
    ///
    /// [解放の規約] Load したものは <see cref="Release"/>、Instantiate したものは
    /// <see cref="ReleaseInstance"/> で必ず返す（Addressables は参照カウント式のため、
    /// 返し忘れはメモリリークになる）。フェーズ単位でまとめて解放する運用を推奨。
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>アセットを読み込む（key の意味は実装依存。Addressables ならアドレス）。</summary>
        UniTask<T> LoadAsync<T>(string key, CancellationToken cancellation = default)
            where T : Object;

        /// <summary>プレハブを読み込んで実体化する（ロードと寿命を実装側が対で管理する）。</summary>
        UniTask<GameObject> InstantiateAsync(string key, Transform parent = null,
            CancellationToken cancellation = default);

        /// <summary>LoadAsync で得たアセットを返却する。</summary>
        void Release(Object asset);

        /// <summary>InstantiateAsync で得た実体を破棄・返却する。</summary>
        void ReleaseInstance(GameObject instance);
    }
}
