using System;

namespace Seed.Core
{
    /// <summary>
    /// ゲームロジック上の想定外（設計バグ）。黙って握り潰さず早期に落として検知する。
    /// 「ゲーム的に正常な失敗」（対象がすでに倒れていた等）はこれではなく SectionResult で返すこと。
    /// </summary>
    public sealed class LogicException : Exception
    {
        /// <summary>LogicException を生成する。</summary>
        public LogicException(string message) : base(message)
        {
        }
    }
}
