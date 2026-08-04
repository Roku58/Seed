using System;
using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// 全キャラクターの一括管理（全体管理）。
    /// 陣営 Manager（PlayersManager / EnemiesManager …）を登録順に束ね、
    /// Tick(dt) 1回で「プレイヤー陣営 → エネミー陣営」という決定的順序の駆動を提供する。
    /// 陣営をまたいだユニット検索と、リアクション命令の順序采配の窓口でもある。
    ///
    /// リアクションの2フェーズ規約:
    /// Tick 実行中に届いたリアクション命令は即時適用せずキューへ積み、
    /// 全陣営の Tick 完了後に届いた順で一括適用する。
    /// これにより「既に Tick 済みのユニットと未 Tick のユニットで同一フレームの
    /// 観測結果が変わる」順序非決定を基盤側で封じる（合成ルートの行儀に頼らない）。
    ///
    /// 合成ルートは本クラスだけを毎フレーム叩けばよく、
    /// 陣営の追加（例: 中立NPC陣営）は AddManager 1行で済む。
    /// </summary>
    public sealed class CharactersManager
    {
        /// <summary>保留中のリアクション1件。</summary>
        private readonly struct PendingReaction
        {
            /// <summary>対象キャラクター。</summary>
            public readonly CharacterId Target;

            /// <summary>リアクション。</summary>
            public readonly ReactionId Reaction;

            /// <summary>荷物。</summary>
            public readonly int Payload;

            /// <summary>PendingReaction を生成する。</summary>
            public PendingReaction(CharacterId target, ReactionId reaction, int payload)
            {
                Target = target;
                Reaction = reaction;
                Payload = payload;
            }
        }

        /// <summary>陣営 Manager（登録順 = Tick 順）。</summary>
        private readonly List<UnitManager> _managers = new List<UnitManager>(2);

        /// <summary>Tick 中に届いたリアクションの待機列。</summary>
        private readonly List<PendingReaction> _pendingReactions = new List<PendingReaction>(4);

        /// <summary>Tick 実行中か。</summary>
        private bool _isTicking;

        /// <summary>CharactersManager を生成する。</summary>
        public CharactersManager(CharacterRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>全陣営が共有する在籍名簿（ICharacterQuery / ICharacterRoster の実体）。</summary>
        public CharacterRegistry Registry { get; }

        /// <summary>陣営 Manager 数。</summary>
        public int ManagerCount => _managers.Count;

        /// <summary>陣営 Manager を登録する（登録順が Tick 順になる）。二重登録は構成ミスとして例外。</summary>
        public void AddManager(UnitManager manager)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }
            if (_managers.Contains(manager))
            {
                throw new HubException("同一の陣営Managerが二重登録された（合成ルートの構成ミス）");
            }
            _managers.Add(manager);
        }

        /// <summary>
        /// 全陣営を登録順に1Tick進め、Tick 中に届いたリアクションを最後に一括適用する
        /// （1フレームの駆動入口）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            _isTicking = true;
            try
            {
                for (var i = 0; i < _managers.Count; i++)
                {
                    _managers[i].Tick(deltaTime);
                }
            }
            finally
            {
                _isTicking = false;
            }
            FlushReactions();
        }

        /// <summary>
        /// リアクション命令の受付口（CharacterSystem が Hub から中継する）。
        /// Tick 中なら順序保護のため保留し、Tick 外なら即時適用する。
        /// 退場済みキャラクターへの命令は正常系として静かに無視する（演出遅延中の死亡など）。
        /// </summary>
        public void PostReaction(CharacterId target, ReactionId reaction, int payload = 0)
        {
            if (_isTicking)
            {
                _pendingReactions.Add(new PendingReaction(target, reaction, payload));
                return;
            }
            ApplyReaction(target, reaction, payload);
        }

        /// <summary>陣営をまたいでユニットを探す。</summary>
        public bool TryGetController(CharacterId id, out UnitController controller)
        {
            for (var i = 0; i < _managers.Count; i++)
            {
                if (_managers[i].TryGet(id, out controller))
                {
                    return true;
                }
            }
            controller = null;
            return false;
        }

        /// <summary>全陣営の全ユニットを外す（シーン終了時）。保留リアクションも破棄する。</summary>
        public void Clear()
        {
            for (var i = 0; i < _managers.Count; i++)
            {
                _managers[i].Clear();
            }
            _pendingReactions.Clear();
        }

        /// <summary>保留中のリアクションを届いた順に適用する。</summary>
        private void FlushReactions()
        {
            // 適用中に届いた分（リアクションの連鎖）も同ループで順に処理する
            for (var i = 0; i < _pendingReactions.Count; i++)
            {
                var pending = _pendingReactions[i];
                ApplyReaction(pending.Target, pending.Reaction, pending.Payload);
            }
            _pendingReactions.Clear();
        }

        /// <summary>リアクション1件を適用する。</summary>
        private void ApplyReaction(CharacterId target, ReactionId reaction, int payload)
        {
            if (Registry.TryGet(target, out var agent))
            {
                agent.PostReaction(reaction, payload);
            }
        }
    }
}
