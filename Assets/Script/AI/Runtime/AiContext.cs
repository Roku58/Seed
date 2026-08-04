using Seed.Character;
using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// 思考1回ぶんの視界（Consideration が見てよい世界の全部）。
    ///
    /// - Frame      … 自分の今（位置・向き・生存・dt）
    /// - Query      … 他者の生存・位置の問い合わせ
    /// - Roster     … 陣営・生存でのユニット列挙（ターゲット選択の土台）
    /// - Blackboard … 自分の記憶（クールダウン・警戒度など）
    /// - Orders     … メタAIからの指示書（居なければ全許可）
    /// </summary>
    public readonly struct AiContext
    {
        /// <summary>自分の今。</summary>
        public readonly LogicFrame Frame;

        /// <summary>他者への問い合わせ窓口。</summary>
        public readonly ICharacterQuery Query;

        /// <summary>陣営列挙の窓口。</summary>
        public readonly ICharacterRoster Roster;

        /// <summary>自分の記憶。</summary>
        public readonly AiBlackboard Blackboard;

        /// <summary>メタAIからの指示書。</summary>
        public readonly AiOrders Orders;

        /// <summary>AiContext を生成する（AiBrain が毎 Think で組む）。</summary>
        public AiContext(in LogicFrame frame, ICharacterQuery query, ICharacterRoster roster,
            AiBlackboard blackboard, in AiOrders orders)
        {
            Frame = frame;
            Query = query;
            Roster = roster;
            Blackboard = blackboard;
            Orders = orders;
        }
    }
}
