using Seed.Hub.Contracts;

namespace Game.Battle.Contracts
{
    /// <summary>
    /// 「このキャラで攻撃したい」という意思（通知）。プレイヤー・AIとも共通の1本
    /// （どちらも「行動開始→アプリが発行」という同じ経路をとるため、発行者で型を分けない）。
    /// 解決の仕方（誰が処理するか）は発行者は知らない。
    ///
    /// 攻守・部位の推測を受け側にさせないため、対象を明示して運ぶ
    /// （Target が None のときだけ、受け側が既定ターゲットを決めてよい）。
    /// </summary>
    public readonly struct AttackRequested : INotificationMessage
    {
        /// <summary>攻撃するキャラクター。</summary>
        public readonly CharacterId Attacker;

        /// <summary>攻撃される側（None なら受け側の既定解決に委ねる）。</summary>
        public readonly CharacterId Target;

        /// <summary>使用モーションのID（ロジック側のエンティティ台帳と同じ値体系）。</summary>
        public readonly int MoveId;

        /// <summary>狙う部位のID（0 = 指定なし。ロジック側のエンティティ台帳と同じ値体系）。</summary>
        public readonly int PartId;

        /// <summary>
        /// 攻撃対象がガード姿勢か（発行時点の行動状態の真実）。
        /// 発行側（アプリ）は対象の Behavior 状態から埋める——受け側が入力状態の写しを
        /// 別経路で持つと「のけぞり中もガード成立」のような実状態との乖離が生まれるため、
        /// ガードの真実はこのメッセージに載せて運ぶ。
        /// </summary>
        public readonly bool TargetGuarding;

        /// <summary>AttackRequested を生成する。</summary>
        public AttackRequested(CharacterId attacker, CharacterId target, int moveId,
            int partId = 0, bool targetGuarding = false)
        {
            Attacker = attacker;
            Target = target;
            MoveId = moveId;
            PartId = partId;
            TargetGuarding = targetGuarding;
        }
    }
}
