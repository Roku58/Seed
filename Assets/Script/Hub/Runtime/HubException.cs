using System;

namespace Seed.Hub
{
    /// <summary>
    /// 仲介基盤の構成ミス（サービス未登録・二重登録など）を早期に検知するための例外。
    /// ゲーム進行中の正常な失敗には使わない。
    /// </summary>
    public sealed class HubException : Exception
    {
        /// <summary>HubException を生成する。</summary>
        public HubException(string message) : base(message)
        {
        }

        /// <summary>元の例外を包んで HubException を生成する（配達中の例外の集約など）。</summary>
        public HubException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
