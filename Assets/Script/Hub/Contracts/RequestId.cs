using System;

namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 「依頼して結果を受け取る」パターンの相関ID（Request-Response 標準）。
    ///
    /// 使い方の規約:
    /// - 依頼側が RequestIdSource.Next() で発番し、命令に載せて発行する
    ///   … XxxRequested(RequestId, …) : ICommandMessage
    /// - 処理側は完了時に同じIDを載せた通知を発行する
    ///   … XxxCompleted(RequestId, 成否, …) : INotificationMessage
    /// - 依頼側は通知を購読し、自分が発行したIDだけを拾う
    /// 命令と応答の対応付けが型規約になるため、複数の依頼が並んでも混線しない。
    /// </summary>
    public readonly struct RequestId : IEquatable<RequestId>
    {
        /// <summary>「依頼なし」を表す予約値。</summary>
        public static readonly RequestId None = new RequestId(0);

        /// <summary>ID値（1以上が有効。RequestIdSource が単調増加で発番する）。</summary>
        public readonly long Value;

        /// <summary>RequestId を生成する（通常は RequestIdSource.Next() を使う）。</summary>
        public RequestId(long value)
        {
            Value = value;
        }

        /// <summary>同一IDかを返す。</summary>
        public bool Equals(RequestId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一IDかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is RequestId other && Equals(other);
        }

        /// <summary>ID値のハッシュ。</summary>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Request#{Value}";
        }
    }
}
