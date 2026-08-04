using Seed.Character;
using Seed.Data;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ユニット1種のマスターデータ（純C#定義）。
    ///
    /// 「キャラを増やす＝この定義を1件足す」の実例。
    /// 数値は本来 ScriptableObject / JSON から MasterDataLoader 経由で流し込むが、
    /// デモではコード上のカタログ（Sample_MasterCatalog）に直書きしている。
    /// 定義ID は CharacterId と同じ値を使い、「どのキャラの仕様か」を素通しで引けるようにする。
    /// </summary>
    public sealed class Sample_UnitSpec : IEntityDefinition
    {
        /// <summary>定義の安定ID（CharacterId と同値運用）。</summary>
        public int Id { get; }

        /// <summary>ログ・エディタ表示用の識別名。</summary>
        public string DebugName { get; }

        /// <summary>移動速度（m/s）。</summary>
        public float MoveSpeed { get; }

        /// <summary>攻撃の拘束秒。</summary>
        public float AttackSeconds { get; }

        /// <summary>のけぞりの拘束秒。</summary>
        public float StaggerSeconds { get; }

        /// <summary>Sample_UnitSpec を生成する。</summary>
        public Sample_UnitSpec(int id, string debugName,
            float moveSpeed, float attackSeconds, float staggerSeconds)
        {
            Id = id;
            DebugName = debugName;
            MoveSpeed = moveSpeed;
            AttackSeconds = attackSeconds;
            StaggerSeconds = staggerSeconds;
        }

        /// <summary>この定義からキャラ基盤の組み立てレシピを作る（データ→Factoryの橋）。</summary>
        public CharacterDefinition ToDefinition()
        {
            return new CharacterDefinition
            {
                MoveSpeed = MoveSpeed,
                AttackSeconds = AttackSeconds,
                StaggerSeconds = StaggerSeconds,
            };
        }
    }
}
