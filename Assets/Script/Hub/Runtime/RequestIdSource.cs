using Seed.Hub.Contracts;

namespace Seed.Hub
{
    /// <summary>
    /// RequestId の発番器（単調増加・重複なし）。
    /// アプリの永続ルートが1つ持ち、依頼を発行する各所へ配る
    /// （複数インスタンスを作るとIDが衝突しうるため、1プロセス1個が規約）。
    /// </summary>
    public sealed class RequestIdSource
    {
        /// <summary>直近に発番した値。</summary>
        private long _last;

        /// <summary>新しい相関IDを発番する。</summary>
        public RequestId Next()
        {
            return new RequestId(++_last);
        }
    }
}
