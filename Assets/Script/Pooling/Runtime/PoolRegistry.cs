using System.Collections.Generic;
using UnityEngine;

namespace Seed.Pooling
{
    /// <summary>
    /// プレハブごとのプールをまとめる台帳。
    ///
    /// プール自体は「1プレハブ＝1プール」だが、ゲームは弾・爆発・足跡・damage 表示と
    /// 何種類も同時に使う。呼び出し側がプールを種類ごとに持ち回るのは配線が増えるので、
    /// 台帳を1つ渡しておき「このプレハブを貸して」で済むようにする。
    ///
    /// 返却は実体に付いた目印（<see cref="PooledInstance"/>）から貸し主を辿るため、
    /// 呼び出し側はどのプールから来たかを覚えておく必要がない。
    ///
    /// 生存期間は所有者（フェーズや永続ルート）に合わせる——<see cref="Clear"/> で
    /// 待機中の実体をまとめて破棄できる。
    /// </summary>
    public sealed class PoolRegistry
    {
        /// <summary>プレハブ → プール。</summary>
        private readonly Dictionary<GameObject, GameObjectPool> _pools =
            new Dictionary<GameObject, GameObjectPool>();

        /// <summary>待機中の実体をぶら下げる親。</summary>
        private readonly Transform _root;

        /// <summary>プールを新設するときの既定の保持上限。</summary>
        private readonly int _defaultMaxRetained;

        /// <summary>PoolRegistry を生成する。</summary>
        public PoolRegistry(Transform root = null, int defaultMaxRetained = 64)
        {
            _root = root;
            _defaultMaxRetained = defaultMaxRetained;
        }

        /// <summary>抱えているプールの数。</summary>
        public int PoolCount => _pools.Count;

        /// <summary>プレハブのプールを取得する（無ければ作る）。</summary>
        public GameObjectPool GetOrCreate(GameObject prefab, int maxRetained = -1)
        {
            if (prefab == null)
            {
                throw new PoolException("null のプレハブに対するプールは作れない");
            }
            if (_pools.TryGetValue(prefab, out var pool))
            {
                return pool;
            }
            pool = new GameObjectPool(prefab, _root,
                maxRetained >= 0 ? maxRetained : _defaultMaxRetained);
            _pools[prefab] = pool;
            return pool;
        }

        /// <summary>借りて配置する（プールが無ければ作られる）。</summary>
        public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            return GetOrCreate(prefab).Rent(position, rotation, parent);
        }

        /// <summary>
        /// 返す。実体の目印から貸し主を辿る。
        /// プール由来でなければ false を返す（呼び出し側が Destroy を選べるようにする）。
        /// </summary>
        public bool Return(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }
            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null || marker.Owner == null)
            {
                return false;
            }
            marker.Owner.Return(instance);
            return true;
        }

        /// <summary>プール由来なら返し、そうでなければ破棄する（呼び分けの手間を省く糖衣）。</summary>
        public void ReturnOrDestroy(GameObject instance)
        {
            if (!Return(instance) && instance != null)
            {
                Object.Destroy(instance);
            }
        }

        /// <summary>まとめて事前生成する（読み込み画面でコストを払っておく）。</summary>
        public void Prewarm(GameObject prefab, int count, int maxRetained = -1)
        {
            GetOrCreate(prefab, maxRetained).Prewarm(count);
        }

        /// <summary>待機中の実体を全プールで破棄する（貸出中には触らない）。</summary>
        public void Clear()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
        }

        /// <summary>
        /// 統計の一覧を取る（プール量の調整・リークの発見に使う。
        /// 貸出が返らずピークが上がり続けるならどこかで返却漏れが起きている）。
        /// </summary>
        public List<(string Prefab, PoolStats Stats)> Snapshot()
        {
            var list = new List<(string, PoolStats)>(_pools.Count);
            foreach (var pair in _pools)
            {
                list.Add((pair.Key != null ? pair.Key.name : "(missing)", pair.Value.Stats));
            }
            return list;
        }
    }
}
