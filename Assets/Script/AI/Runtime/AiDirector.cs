using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// メタAI（ディレクター）の基底。
    ///
    /// 個々のキャラクターAIが「自分の最善」を考えるのに対し、メタAIは戦場全体を見て
    /// 「体験としてどうあるべきか」を采配する——攻撃の順番待ち（攻撃権）、
    /// 難易度の手心（攻撃性の増減）、注目対象の割り当てなど。
    ///
    /// 采配の伝達は指示書（AiOrders）のみ。ユニットを直接動かさず、
    /// 思考への入力を変えるだけなので、メタAIを外しても各ユニットは自律で動き続ける
    /// （IOrderSource を NullOrderSource に差し替えるだけ）。
    /// 具体的な采配ルール（いつ許可するか・どれだけ手心するか）は方針＝アプリ側の派生が持つ。
    /// </summary>
    public abstract class AiDirector : IOrderSource
    {
        /// <summary>ユニットごとの現在の指示書。</summary>
        private readonly Dictionary<CharacterId, AiOrders> _orders =
            new Dictionary<CharacterId, AiOrders>();

        /// <summary>該当ユニットへの指示書（未発行なら全許可）。</summary>
        public AiOrders GetOrders(CharacterId id)
        {
            return _orders.TryGetValue(id, out var orders) ? orders : AiOrders.Default;
        }

        /// <summary>指示書を発行する（派生の Tick から呼ぶ）。</summary>
        protected void SetOrders(CharacterId id, in AiOrders orders)
        {
            _orders[id] = orders;
        }

        /// <summary>指示書を取り下げる（全許可へ戻す）。</summary>
        protected void ClearOrders(CharacterId id)
        {
            _orders.Remove(id);
        }

        /// <summary>采配を1フレーム進める（合成ルートがユニットTickより前に呼ぶ）。</summary>
        public abstract void Tick(float deltaTime);
    }
}
