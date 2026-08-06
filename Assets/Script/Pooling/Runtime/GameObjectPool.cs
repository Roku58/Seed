using UnityEngine;

namespace Seed.Pooling
{
    /// <summary>
    /// プールから貸し出された実体に付く目印（どのプールへ返すかを覚えておく）。
    ///
    /// 返却時に「元のプール」を呼び出し側が持ち回るのは面倒で、持ち回りを間違えると
    /// 別のプールへ混ざる事故になる。実体自身に貸し主を書いておけば、
    /// <see cref="PoolRegistry.Return"/> に渡すだけで正しい場所へ帰る。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour
    {
        /// <summary>貸し主のプール（プール外で生成された実体では null）。</summary>
        public GameObjectPool Owner { get; internal set; }
    }

    /// <summary>
    /// GameObject（プレハブ）のプール。
    ///
    /// 弾・エフェクト・ダメージ表示・生成ステージのタイルのように「短命で大量」の
    /// 実体を Instantiate / Destroy し続けると、生成コストと GC の山が毎フレーム立つ。
    /// 使い終わったら非表示にして待機列へ戻し、次の要求で再利用する。
    ///
    /// [返却時にすること] 非表示化・親の付け戻し・<see cref="IPoolable"/> への通知。
    /// 位置や向きは貸出時に上書きするので触らない——前回の姿勢が見える瞬間を作らないため、
    /// 非表示のまま待機させるのが要点。
    ///
    /// 判断（いつ返すか）は呼び出し側の方針で、本クラスは仕組みだけを持つ。
    /// </summary>
    public sealed class GameObjectPool
    {
        /// <summary>複製元のプレハブ。</summary>
        private readonly GameObject _prefab;

        /// <summary>待機中の実体をぶら下げる親（シーンの見通しを保つため）。</summary>
        private readonly Transform _root;

        /// <summary>実体の貸し借り本体。</summary>
        private readonly ObjectPool<GameObject> _pool;

        /// <summary>
        /// GameObjectPool を生成する。root を渡すと待機中の実体がその下にまとまる
        /// （渡さない場合はシーン直下に置かれる）。
        /// </summary>
        public GameObjectPool(GameObject prefab, Transform root = null, int maxRetained = 64)
        {
            if (prefab == null)
            {
                throw new PoolException("プレハブが null のプールは作れない");
            }
            _prefab = prefab;
            _root = root;
            _pool = new ObjectPool<GameObject>(CreateInstance, maxRetained,
                onRent: null, onReturn: Deactivate);
        }

        /// <summary>複製元のプレハブ（台帳の照合に使う）。</summary>
        public GameObject Prefab => _prefab;

        /// <summary>現在の統計。</summary>
        public PoolStats Stats => _pool.Stats;

        /// <summary>あらかじめ生成しておく（戦闘開始前などに呼ぶ）。</summary>
        public void Prewarm(int count)
        {
            _pool.Prewarm(count);
        }

        /// <summary>借りて配置する（親を渡すとその下へ付け替える）。</summary>
        public GameObject Rent(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var instance = _pool.Rent();
            var transform = instance.transform;
            transform.SetParent(parent != null ? parent : _root, false);
            transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            // 有効化の後に通知する（OnEnable より後に初期化したい実装のため）
            var poolables = instance.GetComponents<IPoolable>();
            for (var i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnRent();
            }
            return instance;
        }

        /// <summary>返す（非表示化して待機列へ。上限超過なら破棄される）。</summary>
        public void Return(GameObject instance)
        {
            _pool.Return(instance);
        }

        /// <summary>待機中を全部破棄する（フェーズ退場時。貸出中には触らない）。</summary>
        public void Clear()
        {
            _pool.Clear(instance =>
            {
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            });
        }

        /// <summary>プレハブを複製して目印を付ける（非表示で待機列へ入る）。</summary>
        private GameObject CreateInstance()
        {
            var instance = Object.Instantiate(_prefab, _root);
            instance.name = _prefab.name; // "(Clone)" を外して Hierarchy を読みやすくする
            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledInstance>();
            }
            marker.Owner = this;
            instance.SetActive(false);
            return instance;
        }

        /// <summary>返却時の後始末（通知 → 非表示 → 親を戻す）。</summary>
        private void Deactivate(GameObject instance)
        {
            if (instance == null)
            {
                return; // シーン破棄などで先に消えている場合は何もしない
            }
            var poolables = instance.GetComponents<IPoolable>();
            for (var i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnReturn();
            }
            instance.SetActive(false);
            instance.transform.SetParent(_root, false);
        }
    }
}
