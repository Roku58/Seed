using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Seed.Pooling.Tests
{
    /// <summary>オブジェクトプール（貸し借りの規約・統計・GameObject 版）のテスト。</summary>
    public sealed class PoolingTests
    {
        /// <summary>テスト用の使い回される玩具（貸出/返却の通知を数える）。</summary>
        private sealed class Toy : IPoolable
        {
            /// <summary>貸出通知の回数。</summary>
            public int RentCount;

            /// <summary>返却通知の回数。</summary>
            public int ReturnCount;

            /// <summary>使用中に汚れる値（リセット漏れの検証用）。</summary>
            public int Dirty;

            /// <summary>貸出時の初期化。</summary>
            public void OnRent()
            {
                RentCount++;
            }

            /// <summary>返却時のリセット。</summary>
            public void OnReturn()
            {
                ReturnCount++;
                Dirty = 0;
            }
        }

        /// <summary>テスト中に作った GameObject（TearDown で確実に消す）。</summary>
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>EditMode では Destroy が遅延しないよう DestroyImmediate で片付ける。</summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();
        }

        // ================================================================
        // 純C#プール
        // ================================================================

        /// <summary>返したものを再利用する（新規生成が増えない）。</summary>
        [Test]
        public void Pool_ReusesReturnedInstance()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            var first = pool.Rent();
            pool.Return(first);
            var second = pool.Rent();

            Assert.AreSame(first, second, "待機中のものが再利用される");
            Assert.AreEqual(1, pool.Stats.Created, "生成は1回だけ");
        }

        /// <summary>IPoolable の貸出・返却通知が呼ばれ、状態がリセットされる。</summary>
        [Test]
        public void Pool_NotifiesPoolable_AndResets()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            var toy = pool.Rent();
            toy.Dirty = 42;
            pool.Return(toy);

            Assert.AreEqual(1, toy.RentCount, "貸出通知が1回");
            Assert.AreEqual(1, toy.ReturnCount, "返却通知が1回");
            Assert.AreEqual(0, toy.Dirty, "返却時にリセットされる");

            pool.Rent();
            Assert.AreEqual(2, toy.RentCount, "再貸出でまた通知される");
        }

        /// <summary>二重返却は即例外（同じ実体が2箇所で使われる事故を発生点で止める）。</summary>
        [Test]
        public void Pool_DoubleReturn_Throws()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            var toy = pool.Rent();
            pool.Return(toy);

            Assert.Throws<PoolException>(() => pool.Return(toy), "二重返却は許さない");
        }

        /// <summary>このプールが貸していないものの返却も例外（プール間の混入を防ぐ）。</summary>
        [Test]
        public void Pool_ForeignReturn_Throws()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            Assert.Throws<PoolException>(() => pool.Return(new Toy()), "他所のものは受け取らない");
            Assert.Throws<PoolException>(() => pool.Return(null), "null も弾く");
        }

        /// <summary>保持上限を超える返却は捨てられる（メモリの際限ない成長を防ぐ）。</summary>
        [Test]
        public void Pool_OverMaxRetained_DiscardsExtra()
        {
            var pool = new ObjectPool<Toy>(() => new Toy(), maxRetained: 1);
            var a = pool.Rent();
            var b = pool.Rent();
            pool.Return(a);
            pool.Return(b); // 上限1なので捨てられる

            Assert.AreEqual(1, pool.Stats.Idle, "待機は上限まで");
            Assert.AreEqual(1, pool.Stats.Discarded, "超過分は破棄として数える");
        }

        /// <summary>Prewarm で先に確保し、貸出時に新規生成しない。</summary>
        [Test]
        public void Pool_Prewarm_AvoidsRuntimeAllocation()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            pool.Prewarm(3);
            Assert.AreEqual(3, pool.Stats.Created, "先に3つ作る");
            Assert.AreEqual(3, pool.Stats.Idle);

            pool.Rent();
            pool.Rent();
            Assert.AreEqual(3, pool.Stats.Created, "貸出では増えない");
        }

        /// <summary>Prewarm は保持上限を超えない。</summary>
        [Test]
        public void Pool_Prewarm_RespectsMaxRetained()
        {
            var pool = new ObjectPool<Toy>(() => new Toy(), maxRetained: 2);
            pool.Prewarm(10);
            Assert.AreEqual(2, pool.Stats.Idle, "上限で止まる");
        }

        /// <summary>同時貸出のピークを記録する（必要なプール量の目安になる）。</summary>
        [Test]
        public void Pool_TracksPeakRented()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            var a = pool.Rent();
            var b = pool.Rent();
            var c = pool.Rent();
            pool.Return(a);
            pool.Return(b);
            pool.Return(c);

            Assert.AreEqual(3, pool.Stats.PeakRented, "同時3つがピーク");
            Assert.AreEqual(0, pool.Stats.Rented, "全部返っている");
        }

        /// <summary>Clear は待機中だけを捨て、貸出中には触らない。</summary>
        [Test]
        public void Pool_Clear_LeavesRentedAlone()
        {
            var pool = new ObjectPool<Toy>(() => new Toy());
            var kept = pool.Rent();          // 貸出中
            pool.Return(pool.Rent());        // 待機へ1つ
            var disposed = 0;

            pool.Clear(_ => disposed++);

            Assert.AreEqual(1, disposed, "待機中の1つが後始末された");
            Assert.AreEqual(0, pool.Stats.Idle);
            Assert.IsTrue(pool.IsRented(kept), "貸出中は生き残る");
        }

        /// <summary>工場が null を返す構成ミスは即例外。</summary>
        [Test]
        public void Pool_NullFactoryResult_Throws()
        {
            var pool = new ObjectPool<Toy>(() => null);
            Assert.Throws<PoolException>(() => pool.Rent());
        }

        // ================================================================
        // GameObject プール
        // ================================================================

        /// <summary>テスト用のプレハブ代わりの GameObject を作る。</summary>
        private GameObject CreatePrefab(string name)
        {
            var prefab = new GameObject(name);
            _spawned.Add(prefab);
            return prefab;
        }

        /// <summary>貸出で有効化・返却で無効化され、実体が再利用される。</summary>
        [Test]
        public void GameObjectPool_RentActivates_ReturnDeactivates()
        {
            var prefab = CreatePrefab("Bullet");
            var pool = new GameObjectPool(prefab);

            var first = pool.Rent(new Vector3(1f, 2f, 3f), Quaternion.identity);
            _spawned.Add(first);
            Assert.IsTrue(first.activeSelf, "貸出で有効化される");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), first.transform.position, "位置が反映される");

            pool.Return(first);
            Assert.IsFalse(first.activeSelf, "返却で無効化される");

            var second = pool.Rent(Vector3.zero, Quaternion.identity);
            Assert.AreSame(first, second, "同じ実体が再利用される");
            Assert.AreEqual(1, pool.Stats.Created, "生成は1回だけ");
        }

        /// <summary>貸し出した実体には貸し主の目印が付く（返却先を辿れる）。</summary>
        [Test]
        public void GameObjectPool_MarksOwner()
        {
            var prefab = CreatePrefab("Effect");
            var pool = new GameObjectPool(prefab);
            var instance = pool.Rent(Vector3.zero, Quaternion.identity);
            _spawned.Add(instance);

            var marker = instance.GetComponent<PooledInstance>();
            Assert.IsNotNull(marker, "目印が付く");
            Assert.AreSame(pool, marker.Owner, "貸し主が記録される");
        }

        /// <summary>台帳は同じプレハブに対して同じプールを返す。</summary>
        [Test]
        public void Registry_ReusesPoolPerPrefab()
        {
            var prefab = CreatePrefab("Shared");
            var registry = new PoolRegistry();

            var a = registry.GetOrCreate(prefab);
            var b = registry.GetOrCreate(prefab);

            Assert.AreSame(a, b, "プレハブごとに1つ");
            Assert.AreEqual(1, registry.PoolCount);
        }

        /// <summary>台帳経由の返却は目印から貸し主へ帰り、プール外は false になる。</summary>
        [Test]
        public void Registry_ReturnsToOwner_AndRejectsForeign()
        {
            var prefab = CreatePrefab("Tile");
            var registry = new PoolRegistry();
            var instance = registry.Rent(prefab, Vector3.zero, Quaternion.identity);
            _spawned.Add(instance);

            Assert.IsTrue(registry.Return(instance), "プール由来は返せる");
            Assert.IsFalse(instance.activeSelf, "非表示になっている");

            var outsider = CreatePrefab("Outsider");
            Assert.IsFalse(registry.Return(outsider), "プール外は false（呼び出し側が破棄を選べる）");
        }

        /// <summary>統計の一覧が取れる（返却漏れの発見に使う）。</summary>
        [Test]
        public void Registry_Snapshot_ReportsStats()
        {
            var prefab = CreatePrefab("Spark");
            var registry = new PoolRegistry();
            var a = registry.Rent(prefab, Vector3.zero, Quaternion.identity);
            var b = registry.Rent(prefab, Vector3.zero, Quaternion.identity);
            _spawned.Add(a);
            _spawned.Add(b);

            var snapshot = registry.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("Spark", snapshot[0].Prefab);
            Assert.AreEqual(2, snapshot[0].Stats.Rented, "2つ貸出中");
            Assert.AreEqual(2, snapshot[0].Stats.PeakRented);
        }
    }
}
