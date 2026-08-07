using Cysharp.Threading.Tasks;

namespace Seed.Flow
{
    /// <summary>
    /// UniTask を <see cref="IFlowOperation"/> に橋渡しするアダプタ。
    ///
    /// フェーズ遷移のロード処理（シーン・アセット・セーブ読込）を async/await で書き、
    /// そのまま <c>CreateLoadOperation</c> から返せるようにする。
    /// GameFlow 側の契約（IsDone のポーリング）は変えない——純C#テストは今まで通り
    /// 偽の IFlowOperation で1Tickずつ検証でき、UniTask は「殻」に留まる。
    /// </summary>
    public sealed class UniTaskFlowOperation : IFlowOperation
    {
        /// <summary>完了したか。</summary>
        private bool _isDone;

        /// <summary>進捗（Report で更新。未報告なら完了時に1へ跳ぶ）。</summary>
        private float _progress;

        /// <summary>UniTask を包む（生成した瞬間から走る）。</summary>
        public UniTaskFlowOperation(UniTask task)
        {
            Await(task).Forget();
        }

        /// <summary>完了したか。</summary>
        public bool IsDone => _isDone;

        /// <summary>進捗（0〜1）。</summary>
        public float Progress => _isDone ? 1f : _progress;

        /// <summary>進捗を報告する（ロード処理側が任意で呼ぶ）。</summary>
        public void Report(float progress)
        {
            _progress = progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        /// <summary>
        /// 完了を監視する。例外でも IsDone は立てる——遷移が永遠に終わらないより、
        /// 入場してから壊れている方が原因に気づけるため（例外自体は UniTask が報告する）。
        /// </summary>
        private async UniTaskVoid Await(UniTask task)
        {
            try
            {
                await task;
            }
            finally
            {
                _isDone = true;
            }
        }
    }
}
