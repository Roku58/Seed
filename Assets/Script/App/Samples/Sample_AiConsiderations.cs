using Seed.AI;
using Seed.Character;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App
{
    /// <summary>【サンプル】ターゲット探索の共通手順（陣営の先頭の生存者。指示書の注目対象が優先）。</summary>
    public static class Sample_AiTargeting
    {
        /// <summary>ターゲットとその位置を探す（居なければ false）。</summary>
        public static bool TryFindTarget(in AiContext context, FactionId targetFaction,
            CharacterId[] buffer, out CharacterId target, out Vector3 position)
        {
            target = context.Orders.FocusTarget;
            if (target.Equals(CharacterId.None) || !context.Query.IsAlive(target))
            {
                target = context.Roster.Query(targetFaction, aliveOnly: true, buffer) > 0
                    ? buffer[0]
                    : CharacterId.None;
            }
            if (!target.Equals(CharacterId.None)
                && context.Query.TryGetPosition(target, out var hubPosition))
            {
                position = new Vector3(hubPosition.X, hubPosition.Y, hubPosition.Z);
                return true;
            }
            position = Vector3.zero;
            return false;
        }
    }

    /// <summary>
    /// 【サンプル】追跡: 相手が遠ければ近づく（攻撃性が高いほどやりたい度が上がる）。
    /// プレイヤーの自動操縦にも敵AIにも同じクラスが使える＝制御共通化の実例。
    /// </summary>
    public sealed class Sample_ChaseConsideration : IAiConsideration
    {
        /// <summary>狙う陣営。</summary>
        private readonly FactionId _targetFaction;

        /// <summary>これ以上は近づかない距離（m）。</summary>
        private readonly float _stopDistance;

        /// <summary>列挙用バッファ（使い回し）。</summary>
        private readonly CharacterId[] _buffer = new CharacterId[8];

        /// <summary>Sample_ChaseConsideration を生成する。</summary>
        public Sample_ChaseConsideration(FactionId targetFaction, float stopDistance)
        {
            _targetFaction = targetFaction;
            _stopDistance = stopDistance;
        }

        /// <summary>相手が停止距離より遠いときだけ参加する。</summary>
        public float Score(in AiContext context)
        {
            if (!Sample_AiTargeting.TryFindTarget(in context, _targetFaction, _buffer, out _, out var position))
            {
                return 0f;
            }
            var distance = Vector3.Distance(context.Frame.SelfPosition, position);
            if (distance <= _stopDistance)
            {
                return 0f;
            }
            return 50f * context.Orders.AggressionPermille / 1000f;
        }

        /// <summary>相手の方向への移動意図（入力と同じ語彙）。</summary>
        public CharacterIntent Act(in AiContext context)
        {
            if (!Sample_AiTargeting.TryFindTarget(in context, _targetFaction, _buffer, out _, out var position))
            {
                return CharacterIntent.None;
            }
            var direction = position - context.Frame.SelfPosition;
            direction.y = 0f;
            return new CharacterIntent(direction.normalized, false, BehaviorKey.None, 0);
        }
    }

    /// <summary>
    /// 【サンプル】攻撃: 射程内かつメタAIの攻撃権があるときに振る。
    /// 「いつ攻撃してよいか」はメタAI（AiOrders.AttackPermitted）が采配し、ここは従うだけ。
    /// </summary>
    public sealed class Sample_AttackConsideration : IAiConsideration
    {
        /// <summary>狙う陣営。</summary>
        private readonly FactionId _targetFaction;

        /// <summary>使う技のID。</summary>
        private readonly int _moveId;

        /// <summary>射程（m）。</summary>
        private readonly float _range;

        /// <summary>列挙用バッファ（使い回し）。</summary>
        private readonly CharacterId[] _buffer = new CharacterId[8];

        /// <summary>Sample_AttackConsideration を生成する。</summary>
        public Sample_AttackConsideration(FactionId targetFaction, int moveId, float range)
        {
            _targetFaction = targetFaction;
            _moveId = moveId;
            _range = range;
        }

        /// <summary>攻撃権があり、射程内に相手がいるときだけ参加する（追跡より強い得点）。</summary>
        public float Score(in AiContext context)
        {
            if (!context.Orders.AttackPermitted)
            {
                return 0f;
            }
            if (!Sample_AiTargeting.TryFindTarget(in context, _targetFaction, _buffer, out _, out var position))
            {
                return 0f;
            }
            return Vector3.Distance(context.Frame.SelfPosition, position) <= _range ? 100f : 0f;
        }

        /// <summary>攻撃の意図（技IDを荷物で運ぶ。入力と同じ語彙）。</summary>
        public CharacterIntent Act(in AiContext context)
        {
            return new CharacterIntent(Vector3.zero, false, BehaviorKey.Attack, _moveId);
        }
    }
}
