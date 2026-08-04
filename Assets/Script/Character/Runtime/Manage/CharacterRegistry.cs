using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// CharacterId → CharacterAgent の在籍名簿。
    /// 契約の ICharacterQuery / ICharacterRoster を実装し、
    /// 他基盤へは「問い合わせ・列挙の窓口」としてだけ見える。
    /// 登録・抹消は Manager（UnitManager.Add/Remove）経由に一本化されており、
    /// GameObject の生死とは無関係に決定的なタイミングで増減する。
    /// 位置の源泉は ActiveActor の ActorPose（純C#）なので、問い合わせまで含めてテストできる。
    /// </summary>
    public sealed class CharacterRegistry : ICharacterQuery, ICharacterRoster
    {
        /// <summary>在籍1件（エージェント＋所属陣営）。</summary>
        private readonly struct Entry
        {
            /// <summary>エージェント本体。</summary>
            public readonly CharacterAgent Agent;

            /// <summary>所属陣営。</summary>
            public readonly FactionId Faction;

            /// <summary>Entry を生成する。</summary>
            public Entry(CharacterAgent agent, FactionId faction)
            {
                Agent = agent;
                Faction = faction;
            }
        }

        /// <summary>在籍中のエージェント。</summary>
        private readonly Dictionary<CharacterId, Entry> _entries =
            new Dictionary<CharacterId, Entry>();

        /// <summary>列挙順を決定的にするための在籍順リスト。</summary>
        private readonly List<CharacterId> _order = new List<CharacterId>(8);

        /// <summary>在籍数。</summary>
        public int Count => _entries.Count;

        /// <summary>エージェントを陣営付きで登録する。同一IDの二重登録は構成ミスとして例外。</summary>
        public void Register(CharacterAgent agent, FactionId faction)
        {
            if (_entries.ContainsKey(agent.Id))
            {
                throw new HubException($"{agent.Id} は登録済み（IDの重複は合成ルートの構成ミス）");
            }
            _entries.Add(agent.Id, new Entry(agent, faction));
            _order.Add(agent.Id);
        }

        /// <summary>エージェントを名簿から外す。</summary>
        public bool Unregister(CharacterId id)
        {
            if (!_entries.Remove(id))
            {
                return false;
            }
            _order.Remove(id);
            return true;
        }

        /// <summary>エージェントの取得を試みる（退場済みなら false）。</summary>
        public bool TryGet(CharacterId id, out CharacterAgent agent)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                agent = entry.Agent;
                return true;
            }
            agent = null;
            return false;
        }

        /// <summary>該当キャラクターが在籍し生存しているか。</summary>
        public bool IsAlive(CharacterId id)
        {
            return _entries.TryGetValue(id, out var entry) && entry.Agent.IsAlive;
        }

        /// <summary>位置の取得を試みる（表示中 Actor の姿勢を契約層のベクトルで返す）。</summary>
        public bool TryGetPosition(CharacterId id, out HubVector3 position)
        {
            if (_entries.TryGetValue(id, out var entry) && entry.Agent.ActiveActor != null)
            {
                var p = entry.Agent.ActiveActor.Pose.Position;
                position = new HubVector3(p.x, p.y, p.z);
                return true;
            }
            position = HubVector3.Zero;
            return false;
        }

        /// <summary>
        /// 条件に合うキャラクターIDを buffer へ詰め、詰めた件数を返す（在籍順＝決定的）。
        /// faction に FactionId.Any を渡すと陣営を問わない。
        /// </summary>
        public int Query(FactionId faction, bool aliveOnly, CharacterId[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }
            var written = 0;
            for (var i = 0; i < _order.Count && written < buffer.Length; i++)
            {
                var entry = _entries[_order[i]];
                if (!faction.Equals(FactionId.Any) && !entry.Faction.Equals(faction))
                {
                    continue;
                }
                if (aliveOnly && !entry.Agent.IsAlive)
                {
                    continue;
                }
                buffer[written++] = _order[i];
            }
            return written;
        }

        /// <summary>該当キャラクターの陣営（不在なら FactionId.None）。</summary>
        public FactionId GetFaction(CharacterId id)
        {
            return _entries.TryGetValue(id, out var entry) ? entry.Faction : FactionId.None;
        }
    }
}
