namespace Seed.Flow
{
    /// <summary>
    /// シーン（Unityの.unityアセット）ロードの抽象。
    /// フェーズの CreateLoadOperation から使い、実装（UnitySceneLoader）は
    /// 合成ルートがコンストラクタ注入する——フェーズを純C#テストするときは
    /// 偽実装に差し替えられる。
    /// プリミティブ生成だけで済むデモはこれを使わなくてよい
    /// （ステージ=シーンアセットになった時の差し込み口）。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>シーンを非同期ロードする（additive=true で現シーンに重ねる）。</summary>
        IFlowOperation LoadScene(string sceneName, bool additive = false);

        /// <summary>シーンを非同期アンロードする（additiveで重ねたものを剥がす）。</summary>
        IFlowOperation UnloadScene(string sceneName);
    }
}
