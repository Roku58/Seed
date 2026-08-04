namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】リプレイ可能な入力（★パターン: InputJournal）。
    /// オブジェクト参照ではなく EntityRegistry のIDで対象を指すため、そのまま直列化できる。
    /// 「時間を進める」も入力として記録することで、再生が単純な先頭からの流し直しになる。
    /// </summary>
    public readonly struct Sample_ActionInput
    {
        /// <summary>種別。</summary>
        public readonly Sample_ActionInputKind Kind;
        /// <summary>モーションのID。</summary>
        public readonly int MoveId;
        /// <summary>部位のID。</summary>
        public readonly int PartId;
        public readonly bool Flag;   // MonsterAttack: ガード成立
        public readonly int Value;   // AdvanceTime: ms / UseDemonDrug: 攻撃+n
        public readonly int Value2;  // UseDemonDrug: 持続ms

        /// <summary>Sample_ActionInput を生成する。</summary>
        private Sample_ActionInput(Sample_ActionInputKind kind, int moveId, int partId,
            bool flag, int value, int value2)
        {
            Kind = kind;
            MoveId = moveId;
            PartId = partId;
            Flag = flag;
            Value = value;
            Value2 = value2;
        }

        /// <summary>時間を進める（ミリ秒）。</summary>
        public static Sample_ActionInput AdvanceTime(int ms)
        {
            return new Sample_ActionInput(Sample_ActionInputKind.AdvanceTime,
                EntityRegistry.None, EntityRegistry.None, false, ms, 0);
        }

        /// <summary>鬼人薬を使用する（Extendポリシーで延長）。</summary>
        public static Sample_ActionInput UseDemonDrug(int attackBonus, int durationMs)
        {
            return new Sample_ActionInput(Sample_ActionInputKind.UseDemonDrug,
                EntityRegistry.None, EntityRegistry.None, false, attackBonus, durationMs);
        }

        /// <summary>ハンターの攻撃（発動判定→ヒット解決）を実行する。</summary>
        public static Sample_ActionInput HunterAttack(int moveId, int partId)
        {
            return new Sample_ActionInput(Sample_ActionInputKind.HunterAttack,
                moveId, partId, false, 0, 0);
        }

        /// <summary>モンスターの攻撃（同じセクションを再利用）を実行する。</summary>
        public static Sample_ActionInput MonsterAttack(int moveId, bool hunterGuarded)
        {
            return new Sample_ActionInput(Sample_ActionInputKind.MonsterAttack,
                moveId, EntityRegistry.None, hunterGuarded, 0, 0);
        }
    }
}
