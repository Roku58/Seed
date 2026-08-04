using System.Collections.Generic;
using Seed.Hub.Contracts;

namespace Seed.AI
{
    /// <summary>
    /// 攻撃権トークン（同時に攻撃してよいユニット数の上限管理）。
    ///
    /// 「敵が10体居ても同時に殴りかかるのは2体まで」というメタAIの定番采配の道具。
    /// ディレクターが所有し、Consideration の許可判断（AiOrders.AttackPermitted）の
    /// 裏付けとして使う。取得は早い者勝ちで決定的（乱数を使わない）。
    /// </summary>
    public sealed class AttackTokenPool
    {
        /// <summary>同時攻撃の上限。</summary>
        private readonly int _maxConcurrent;

        /// <summary>トークン保持者。</summary>
        private readonly HashSet<CharacterId> _holders = new HashSet<CharacterId>();

        /// <summary>AttackTokenPool を生成する。</summary>
        public AttackTokenPool(int maxConcurrent)
        {
            _maxConcurrent = maxConcurrent < 1 ? 1 : maxConcurrent;
        }

        /// <summary>貸し出し中の数。</summary>
        public int ActiveCount => _holders.Count;

        /// <summary>該当ユニットが保持中か。</summary>
        public bool IsHolder(CharacterId id)
        {
            return _holders.Contains(id);
        }

        /// <summary>トークンの取得を試みる（保持済みなら true のまま。満員なら false）。</summary>
        public bool TryAcquire(CharacterId id)
        {
            if (_holders.Contains(id))
            {
                return true;
            }
            if (_holders.Count >= _maxConcurrent)
            {
                return false;
            }
            _holders.Add(id);
            return true;
        }

        /// <summary>トークンを返す（攻撃の終了・死亡時にディレクターが呼ぶ）。</summary>
        public bool Release(CharacterId id)
        {
            return _holders.Remove(id);
        }

        /// <summary>全トークンを回収する（フェーズ再入時など）。</summary>
        public void Clear()
        {
            _holders.Clear();
        }
    }
}
