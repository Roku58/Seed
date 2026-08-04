namespace Seed.Flow
{
    /// <summary>
    /// フェーズ遷移中の非同期作業（シーンロード・アセットロード等）の進捗契約。
    /// GameFlow は IsDone を毎Tickポーリングし、完了したら次フェーズへ入る。
    /// コルーチン・Task に依存しないのは、純C#テストで遷移を1Tickずつ検証できるようにするため。
    /// </summary>
    public interface IFlowOperation
    {
        /// <summary>完了したか。</summary>
        bool IsDone { get; }

        /// <summary>進捗（0〜1。ローディング画面の表示用）。</summary>
        float Progress { get; }
    }

    /// <summary>即座に完了している作業（同期フェーズ・テスト用）。</summary>
    public sealed class CompletedFlowOperation : IFlowOperation
    {
        /// <summary>共有インスタンス。</summary>
        public static readonly CompletedFlowOperation Instance = new CompletedFlowOperation();

        /// <summary>常に完了。</summary>
        public bool IsDone => true;

        /// <summary>常に100%。</summary>
        public float Progress => 1f;
    }
}
