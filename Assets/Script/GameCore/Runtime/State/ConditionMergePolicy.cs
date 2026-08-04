namespace Seed.Core
{
    /// <summary>
    /// 同じ種類のコンディションを重ねて付与したときの扱い。
    /// 「バフを重ねがけしたらどうなるか」はゲーム仕様なので、仕様ごとに明示的に選ぶ。
    /// </summary>
    public enum ConditionMergePolicy
    {
        /// <summary>単純に併存させる（既定の Add と同じ）</summary>
        Stack,
        /// <summary>効果時間を延長する（失効時刻の遅い方を採用。効果量は既存を維持）</summary>
        Extend,
        /// <summary>新しい方で上書きする</summary>
        Overwrite,
        /// <summary>効果量を加算する（失効時刻は遅い方を採用）</summary>
        AddMagnitude,
        /// <summary>すでにあるなら何もしない</summary>
        Ignore,
    }
}
