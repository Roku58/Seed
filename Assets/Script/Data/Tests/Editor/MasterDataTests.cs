using NUnit.Framework;
using Seed.Core;

namespace Seed.Data.Tests
{
    /// <summary>MasterDataSet / MasterDataLoader（マスターデータ→台帳の一本道）のテスト。</summary>
    public sealed class MasterDataTests
    {
        /// <summary>テスト用の武器定義（DTO基底の継承例＝ゲーム側の増やし方そのもの）。</summary>
        private sealed class WeaponDef : EntityDefinitionData
        {
            /// <summary>攻撃力。</summary>
            public int attack;

            /// <summary>WeaponDef を生成する。</summary>
            public WeaponDef(int id, string name, int attack) : base(id, name)
            {
                this.attack = attack;
            }
        }

        /// <summary>テスト用のモンスター定義。</summary>
        private sealed class MonsterDef : EntityDefinitionData
        {
            /// <summary>MonsterDef を生成する。</summary>
            public MonsterDef(int id, string name) : base(id, name)
            {
            }
        }

        /// <summary>型別カタログ: Add / Get / GetAll がID昇順で引ける。</summary>
        [Test]
        public void Catalog_AddGetGetAll_Works()
        {
            var data = new MasterDataSet();
            data.Add(new WeaponDef(102, "尻尾", 30));
            data.Add(new WeaponDef(100, "斬り上げ", 40));
            data.Add(new MonsterDef(2, "リオレイア"));

            Assert.AreEqual(3, data.Count);
            Assert.AreEqual(40, data.Get<WeaponDef>(100).attack);
            Assert.IsTrue(data.TryGet<MonsterDef>(2, out _));
            Assert.IsFalse(data.TryGet<MonsterDef>(9, out _));

            var weapons = data.GetAll<WeaponDef>();
            Assert.AreEqual(2, weapons.Count);
            Assert.AreEqual(100, weapons[0].Id, "GetAll はID昇順（決定的な列挙順）");
        }

        /// <summary>ID重複（型をまたいでも）は検証で構成ミスとして検知される。</summary>
        [Test]
        public void ValidateGlobalIdUniqueness_DetectsCrossTypeDuplicates()
        {
            var data = new MasterDataSet();
            data.Add(new WeaponDef(100, "斬り上げ", 40));
            data.Add(new MonsterDef(100, "ID衝突"));

            Assert.Throws<LogicException>(() => data.ValidateGlobalIdUniqueness());
        }

        /// <summary>RegisterAll: 定義がそのIDのまま EntityRegistry に載る（レコードと同じID体系）。</summary>
        [Test]
        public void RegisterAll_PutsDefinitionsIntoRegistryWithStableIds()
        {
            var data = new MasterDataSet();
            data.Add(new WeaponDef(100, "斬り上げ", 40));
            data.Add(new MonsterDef(2, "リオレイア"));

            var registry = new EntityRegistry();
            new MasterDataLoader().RegisterAll(data, registry);

            Assert.AreEqual(40, registry.GetEntity<WeaponDef>(100).attack, "定義IDのまま引ける");
            Assert.AreEqual(100, registry.GetId(data.Get<WeaponDef>(100)), "逆引きも一致");
            Assert.IsNotNull(registry.GetEntity<MonsterDef>(2));
        }

        /// <summary>ID重複したデータの一括登録は、登録前の検証で止まる。</summary>
        [Test]
        public void RegisterAll_RejectsDuplicateIds_BeforeTouchingRegistry()
        {
            var data = new MasterDataSet();
            data.Add(new WeaponDef(7, "A", 1));
            data.Add(new MonsterDef(7, "B"));
            var registry = new EntityRegistry();

            Assert.Throws<LogicException>(() => new MasterDataLoader().RegisterAll(data, registry));
            Assert.AreEqual(0, registry.Count, "途中まで登録された中途半端な状態にならない");
        }
    }
}
