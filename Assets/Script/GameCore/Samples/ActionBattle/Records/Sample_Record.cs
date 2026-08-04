namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】事象レコード（★パターン: EntityRegistry によるID方式）。
    /// アクター・部位・モーションを参照ではなく整数IDで持つため、
    /// そのまま直列化できる＝セーブ・リプレイ保存・ログ送信・サーバ検証が可能。
    /// 表示時は EntityRegistry で引き直す（Sample_ActionBattlePresenter 参照）。
    /// HP等の変動値はスナップショット（Value2）で焼き込む＝レコードは自己完結が原則。
    /// </summary>
    public readonly struct Sample_Record : IHashableRecord
    {
        /// <summary>発生時刻（ミリ秒）。</summary>
        public readonly long TimeMs;
        /// <summary>種別。</summary>
        public readonly Sample_RecordKind Kind;
        /// <summary>行動主体のID。</summary>
        public readonly int ActorId;
        /// <summary>対象のID。</summary>
        public readonly int TargetId;
        /// <summary>部位のID。</summary>
        public readonly int PartId;
        /// <summary>モーションのID。</summary>
        public readonly int MoveId;
        /// <summary>状態異常種別。</summary>
        public readonly Sample_StatusKind Status;
        /// <summary>コンディション種別。</summary>
        public readonly Sample_ConditionKind Condition;
        /// <summary>行動不可の理由。</summary>
        public readonly Sample_BlockReason Block;
        /// <summary>主値（補正対象・ダメージ量など）。</summary>
        public readonly int Value;
        /// <summary>HP・閾値などのスナップショット</summary>
        public readonly int Value2;
        /// <summary>汎用フラグ（会心・ガードなど）。</summary>
        public readonly bool Flag;

        /// <summary>Sample_Record を生成する。</summary>
        public Sample_Record(long timeMs, Sample_RecordKind kind,
            int actorId = EntityRegistry.None, int targetId = EntityRegistry.None,
            int partId = EntityRegistry.None, int moveId = EntityRegistry.None,
            Sample_StatusKind status = Sample_StatusKind.None,
            Sample_ConditionKind condition = Sample_ConditionKind.None,
            Sample_BlockReason block = Sample_BlockReason.None,
            int value = 0, int value2 = 0, bool flag = false)
        {
            TimeMs = timeMs;
            Kind = kind;
            ActorId = actorId;
            TargetId = targetId;
            PartId = partId;
            MoveId = moveId;
            Status = status;
            Condition = condition;
            Block = block;
            Value = value;
            Value2 = value2;
            Flag = flag;
        }

        /// <summary>
        /// 構造化ハッシュ（★パターン: IHashableRecord）。
        /// 表示文字列ではなくフィールドを直接流し込むため、Presenterの文言修正で
        /// リプレイ検証が壊れない。フィールドは固定順で全て加える。
        /// </summary>
        public void AddTo(ref StableHash hash)
        {
            hash.Add(TimeMs);
            hash.Add((int)Kind);
            hash.Add(ActorId);
            hash.Add(TargetId);
            hash.Add(PartId);
            hash.Add(MoveId);
            hash.Add((int)Status);
            hash.Add((int)Condition);
            hash.Add((int)Block);
            hash.Add(Value);
            hash.Add(Value2);
            hash.Add(Flag);
        }
    }
}
