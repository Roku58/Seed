namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】事象レコード（★パターン: 参照方式）。
    /// アクター等を直接参照で持つ、最も簡単な形。小規模・保存不要ならこれで十分。
    /// セーブ・リプレイ保存・通信が必要なら、ActionBattle サンプルの
    /// 「EntityRegistry によるID方式」を参照すること。
    /// HP等の変動値はスナップショット（Value2）で焼き込む＝レコードは自己完結が原則。
    /// </summary>
    public readonly struct Sample_Record
    {
        /// <summary>ターン番号。</summary>
        public readonly int Turn;
        /// <summary>種別。</summary>
        public readonly Sample_RecordKind Kind;
        /// <summary>行動主体。</summary>
        public readonly Sample_Actor Actor;
        /// <summary>対象。</summary>
        public readonly Sample_Actor Target;
        /// <summary>使用モーション/技。</summary>
        public readonly Sample_MoveData Move;
        /// <summary>コンディション種別。</summary>
        public readonly Sample_ConditionKind Condition;
        /// <summary>とくせい種別。</summary>
        public readonly Sample_AbilityKind Ability;
        /// <summary>どうぐ種別。</summary>
        public readonly Sample_ItemKind Item;
        /// <summary>失敗理由。</summary>
        public readonly Sample_FailReason Reason;
        /// <summary>主値（ダメージ等）</summary>
        public readonly int Value;
        /// <summary>HPスナップショット等</summary>
        public readonly int Value2;
        /// <summary>タイプ相性(‰)。View層が「ばつぐん/いまひとつ」を表示するための情報</summary>
        public readonly int TypePermille;

        /// <summary>Sample_Record を生成する。</summary>
        public Sample_Record(int turn, Sample_RecordKind kind, Sample_Actor actor = null, Sample_Actor target = null,
            Sample_MoveData move = null, Sample_ConditionKind condition = Sample_ConditionKind.None,
            Sample_AbilityKind ability = Sample_AbilityKind.None, Sample_ItemKind item = Sample_ItemKind.None,
            Sample_FailReason reason = Sample_FailReason.None, int value = 0, int value2 = 0,
            int typePermille = Permille.One)
        {
            Turn = turn;
            Kind = kind;
            Actor = actor;
            Target = target;
            Move = move;
            Condition = condition;
            Ability = ability;
            Item = item;
            Reason = reason;
            Value = value;
            Value2 = value2;
            TypePermille = typePermille;
        }
    }
}
