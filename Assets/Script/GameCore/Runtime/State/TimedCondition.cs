using System;

namespace Seed.Core
{
    /// <summary>
    /// 時限コンディション（バフ・状態異常・怒り等）。種別はゲーム側の enum。
    ///
    /// [設計原則: ハンドラーの状態レス化]
    /// 「効果中かどうか」「+いくつか」といった可変状態はハンドラーではなく
    /// アクター側のこの構造体に置く。ハンドラーは毎回これを読んで補正するだけにすると、
    /// セーブ・巻き戻し・ネットワーク同期の対象がアクター状態に一元化される。
    /// </summary>
    public readonly struct TimedCondition<TKind> where TKind : struct, Enum
    {
        /// <summary>種別。</summary>
        public readonly TKind Kind;

        /// <summary>失効時刻（ms）。永続なら long.MaxValue。</summary>
        public readonly long ExpiresAtMs;

        /// <summary>効果量（+15、900‰ など。意味はゲーム側が定義）。</summary>
        public readonly int Magnitude;

        /// <summary>TimedCondition を生成する。</summary>
        public TimedCondition(TKind kind, long expiresAtMs, int magnitude)
        {
            Kind = kind;
            ExpiresAtMs = expiresAtMs;
            Magnitude = magnitude;
        }
    }
}
