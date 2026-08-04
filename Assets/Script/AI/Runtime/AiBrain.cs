using System;
using System.Collections.Generic;
using Seed.Character;
using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// キャラクターAIの頭脳（ユーティリティ型の ICharacterLogic 実装）。
    ///
    /// 毎 Think で全 Consideration の Score を採点し、最高得点の Act が返す
    /// CharacterIntent（＝入力と同じ意図）をそのまま流す。
    /// **制御の経路はプレイヤーと完全に共通**——Controller・Agent・Behavior から見れば
    /// 手動入力（ManualLogic）とAIの区別は存在しない。逆に InputEmulator を使えば、
    /// この頭脳でプレイヤーユニットの ManualLogic へ入力を注入する（自動操縦）こともできる。
    ///
    /// 決定性: 採点は登録順に走査し、同点は先勝ち（登録順が優先度を兼ねる）。
    /// 乱数を使う Consideration はゲーム側の決定的乱数を注入すること。
    /// </summary>
    public sealed class AiBrain : ICharacterLogic
    {
        /// <summary>思考の候補（登録順 = 同点時の優先順）。</summary>
        private readonly List<IAiConsideration> _considerations = new List<IAiConsideration>(4);

        /// <summary>他者への問い合わせ窓口。</summary>
        private readonly ICharacterQuery _query;

        /// <summary>陣営列挙の窓口。</summary>
        private readonly ICharacterRoster _roster;

        /// <summary>メタAIからの指示元。</summary>
        private readonly IOrderSource _orders;

        /// <summary>AiBrain を生成する（orders 省略時は全許可＝完全自律）。</summary>
        public AiBrain(ICharacterQuery query, ICharacterRoster roster,
            IOrderSource orders = null, AiBlackboard blackboard = null)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _orders = orders ?? NullOrderSource.Instance;
            Blackboard = blackboard ?? new AiBlackboard();
        }

        /// <summary>この頭脳の記憶（テスト・デバッグからも覗ける）。</summary>
        public AiBlackboard Blackboard { get; }

        /// <summary>思考の候補を追加する（流れるように書ける糖衣）。</summary>
        public AiBrain With(IAiConsideration consideration)
        {
            if (consideration == null)
            {
                throw new ArgumentNullException(nameof(consideration));
            }
            _considerations.Add(consideration);
            return this;
        }

        /// <summary>採点 → 最高得点の候補の意図を返す（全員不参加なら何もしない）。</summary>
        public CharacterIntent Think(in LogicFrame frame)
        {
            Blackboard.TimeSeconds += frame.DeltaTime; // AI時計（クールダウンの基準）

            var context = new AiContext(in frame, _query, _roster, Blackboard,
                _orders.GetOrders(frame.SelfId));

            IAiConsideration best = null;
            var bestScore = 0f;
            for (var i = 0; i < _considerations.Count; i++)
            {
                var score = _considerations[i].Score(in context);
                if (score > bestScore) // 同点は先勝ち＝登録順が優先度を兼ねる
                {
                    bestScore = score;
                    best = _considerations[i];
                }
            }
            return best != null ? best.Act(in context) : CharacterIntent.None;
        }
    }
}
