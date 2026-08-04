namespace Seed.Core
{
    /// <summary>
    /// セクションの「ゲーム的に正常な失敗」を表す標準の戻り値型。
    ///
    /// 例外（LogicException）は「設計バグ」専用とし、
    /// 「対象がすでに倒れていた」「条件を満たさなかった」のような
    /// 起こりうる失敗はこの型で返す、という使い分けの規約。
    ///
    /// FailureCode の意味（enum）はゲーム側が定義する（int にキャストして渡す）。
    /// if (result) と書けるよう bool への暗黙変換を持つ。
    /// </summary>
    public readonly struct SectionResult
    {
        /// <summary>成功したか。</summary>
        public readonly bool Success;
        /// <summary>失敗理由コード（ゲーム側の enum を int 化して格納。成功時は 0）</summary>
        public readonly int FailureCode;

        /// <summary>SectionResult を生成する。</summary>
        private SectionResult(bool success, int failureCode)
        {
            Success = success;
            FailureCode = failureCode;
        }

        /// <summary>成功の結果を作る。</summary>
        public static SectionResult Ok()
        {
            return new SectionResult(true, 0);
        }

        /// <summary>失敗の結果を作る。</summary>
        public static SectionResult Fail(int failureCode)
        {
            return new SectionResult(false, failureCode);
        }

        /// <summary>真偽値として評価できるようにする（if (result) 判定用）。</summary>
        public static implicit operator bool(SectionResult result)
        {
            return result.Success;
        }
    }

    /// <summary>値付きのセクション結果。</summary>
    public readonly struct SectionResult<TValue>
    {
        /// <summary>成功したか。</summary>
        public readonly bool Success;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public readonly TValue Value;
        /// <summary>失敗理由コード（ゲーム側enumのint）。</summary>
        public readonly int FailureCode;

        /// <summary>SectionResult を生成する。</summary>
        private SectionResult(bool success, TValue value, int failureCode)
        {
            Success = success;
            Value = value;
            FailureCode = failureCode;
        }

        /// <summary>成功の結果を作る。</summary>
        public static SectionResult<TValue> Ok(TValue value)
        {
            return new SectionResult<TValue>(true, value, 0);
        }

        /// <summary>失敗の結果を作る。</summary>
        public static SectionResult<TValue> Fail(int failureCode)
        {
            return new SectionResult<TValue>(false, default, failureCode);
        }

        /// <summary>真偽値として評価できるようにする（if (result) 判定用）。</summary>
        public static implicit operator bool(SectionResult<TValue> result)
        {
            return result.Success;
        }
    }
}
