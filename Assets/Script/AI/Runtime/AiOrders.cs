using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// メタAI（AiDirector）からユニットの思考への指示書。
    ///
    /// キャラクターAIは「自分ならこうしたい」を考え、メタAIは「全体としてどうあるべきか」
    /// （攻撃の順番待ち・難易度の手心・注目対象の割り当て）をこの指示書で伝える。
    /// 指示はあくまで思考への入力であり、従うかどうかの解釈は各 Consideration の実装が持つ。
    /// メタAIが居ない構成では Default（全許可）が使われ、ユニットは完全自律で動く。
    /// </summary>
    public readonly struct AiOrders
    {
        /// <summary>指示なし（全許可・完全自律）。</summary>
        public static readonly AiOrders Default = new AiOrders(
            attackPermitted: true, aggressionPermille: 1000, focusTarget: CharacterId.None);

        /// <summary>攻撃してよいか（攻撃権。メタAIの順番待ち采配）。</summary>
        public readonly bool AttackPermitted;

        /// <summary>攻撃性（‰。1000=全開。難易度の手心はここを絞る）。</summary>
        public readonly int AggressionPermille;

        /// <summary>優先的に狙うべき相手（None なら各自の判断）。</summary>
        public readonly CharacterId FocusTarget;

        /// <summary>AiOrders を生成する。</summary>
        public AiOrders(bool attackPermitted, int aggressionPermille, CharacterId focusTarget)
        {
            AttackPermitted = attackPermitted;
            AggressionPermille = aggressionPermille;
            FocusTarget = focusTarget;
        }
    }

    /// <summary>指示書の取得窓口（実装は AiDirector。メタAI無し構成は NullOrderSource）。</summary>
    public interface IOrderSource
    {
        /// <summary>該当ユニットへの現在の指示書。</summary>
        AiOrders GetOrders(CharacterId id);
    }

    /// <summary>常に全許可を返す指示元（メタAIを置かない構成用）。</summary>
    public sealed class NullOrderSource : IOrderSource
    {
        /// <summary>共有インスタンス。</summary>
        public static readonly NullOrderSource Instance = new NullOrderSource();

        /// <summary>常に Default。</summary>
        public AiOrders GetOrders(CharacterId id)
        {
            return AiOrders.Default;
        }
    }
}
