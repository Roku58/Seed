namespace Seed.Core
{
    /// <summary>
    /// セクション：ゲームロジックの最小単位。
    ///
    /// [講演由来の中核概念]
    /// 「何をどの順番で計算するか」という処理の流れだけを定義し、
    /// 技・スキル・アイテム等の個別仕様は一切含まない。個別仕様は、
    /// セクションが要所で発火するイベントを通じてのみ介入できる。
    ///
    /// - セクションは階層構造を持てる（例: ダメージ付与 ⊃ ダメージ計算 ⊃ 攻撃力決定…）
    /// - 「再利用可能な単位」で分割する。ジャンルが変わっても（ターン制→リアルタイム制）
    ///   セクションを取捨選択・組み替えて流用できる粒度が正しい粒度
    /// - イベントハンドラーからも RunSection で呼び出せる（割り込み・連鎖）
    ///
    /// [設計原則: 入出力の標準化 / 低GC]
    /// 入力・結果は readonly struct で受け渡す。セクション自身は状態を持たないため、
    /// static readonly な Instance を使い回せる（実行ごとの new が不要）。
    /// 実行は必ず LogicContext.RunSection 経由で行うこと（深度ガードのため）。
    /// </summary>
    public abstract class Section<TInput, TResult>
    {
        /// <summary>デバッグ・例外メッセージ用のセクション名。</summary>
        public abstract string Name { get; }

        /// <summary>
        /// セクション本体（LogicContext.RunSection だけが呼ぶ）。
        /// protected internal なのは「派生クラスは実装できるが、外部からの直接呼び出しは
        /// コンパイルエラーになる」ようにするため——深度ガード・トレースを迂回する
        /// 直叩きを型レベルで禁止する。
        /// </summary>
        protected internal abstract TResult Execute(LogicContext ctx, in TInput input);
    }
}
