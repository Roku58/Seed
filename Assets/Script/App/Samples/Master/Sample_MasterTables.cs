using MasterMemory;
using MessagePack;

// MasterMemory の Source Generator が生成する DatabaseBuilder / MemoryDatabase の
// 名前空間を固定する（未指定の既定 'MasterMemory' は衝突しやすいため明示する）
[assembly: MasterMemoryGeneratorOptions(Namespace = "Seed.App.Master")]

namespace Seed.App
{
    /// <summary>
    /// ユニット仕様のバイナリ行（MasterMemory テーブル）。
    ///
    /// [なぜ Spec と別型か] <see cref="Sample_UnitSpec"/> は読み取り専用プロパティの
    /// 純C#（ゲームが使う真実の形）で、こちらは「直列化の都合」を引き受ける入出力用の形。
    /// 直列化ライブラリの要求（可変プロパティ・属性）を真実の型へ侵食させないための分離で、
    /// ScriptableObject 入力（EntityDefinitionAsset）とも同じ二本立ての流儀である。
    /// </summary>
    [MemoryTable("unit"), MessagePackObject(true)]
    public sealed class Sample_UnitRow
    {
        /// <summary>ユニットID（主キー）。</summary>
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>識別名。</summary>
        public string DebugName { get; set; }

        /// <summary>移動速度（m/s）。</summary>
        public float MoveSpeed { get; set; }

        /// <summary>攻撃の拘束時間（秒）。</summary>
        public float AttackSeconds { get; set; }

        /// <summary>怯みの拘束時間（秒）。</summary>
        public float StaggerSeconds { get; set; }

        /// <summary>Spec（真実の形）から行を作る。</summary>
        public static Sample_UnitRow From(Sample_UnitSpec spec)
        {
            return new Sample_UnitRow
            {
                Id = spec.Id,
                DebugName = spec.DebugName,
                MoveSpeed = spec.MoveSpeed,
                AttackSeconds = spec.AttackSeconds,
                StaggerSeconds = spec.StaggerSeconds,
            };
        }

        /// <summary>行から Spec（真実の形）へ戻す。</summary>
        public Sample_UnitSpec ToSpec()
        {
            return new Sample_UnitSpec(Id, DebugName, MoveSpeed, AttackSeconds, StaggerSeconds);
        }
    }

    /// <summary>
    /// ステージ仕様のバイナリ行（MasterMemory テーブル）。
    /// 色は float 3成分で持つ（UnityEngine.Color を直列化契約に載せない＝
    /// バイナリをエンジン非依存に保ち、ヘッドレス検証でも読めるようにする）。
    /// </summary>
    [MemoryTable("stage"), MessagePackObject(true)]
    public sealed class Sample_StageRow
    {
        /// <summary>ステージID（主キー）。</summary>
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>識別名（ログ・エディタ用）。</summary>
        public string DebugName { get; set; }

        /// <summary>表示名（メニュー用）。</summary>
        public string DisplayName { get; set; }

        /// <summary>地面色 R。</summary>
        public float GroundR { get; set; }

        /// <summary>地面色 G。</summary>
        public float GroundG { get; set; }

        /// <summary>地面色 B。</summary>
        public float GroundB { get; set; }

        /// <summary>地面の広さ。</summary>
        public float GroundScale { get; set; }

        /// <summary>敵の基準攻撃間隔（秒）。</summary>
        public float EnemyAttackInterval { get; set; }

        /// <summary>生成種別（0=平地/1=迷路/2=街）。</summary>
        public int GeneratorKind { get; set; }

        /// <summary>生成グリッド幅。</summary>
        public int GenWidth { get; set; }

        /// <summary>生成グリッド高さ。</summary>
        public int GenHeight { get; set; }

        /// <summary>迷路の行き止まり貫通率（‰）。</summary>
        public int BraidPermille { get; set; }

        /// <summary>Spec（真実の形）から行を作る。</summary>
        public static Sample_StageRow From(Sample_StageSpec spec)
        {
            return new Sample_StageRow
            {
                Id = spec.Id,
                DebugName = spec.DebugName,
                DisplayName = spec.DisplayName,
                GroundR = spec.GroundColor.r,
                GroundG = spec.GroundColor.g,
                GroundB = spec.GroundColor.b,
                GroundScale = spec.GroundScale,
                EnemyAttackInterval = spec.EnemyAttackInterval,
                GeneratorKind = spec.GeneratorKind,
                GenWidth = spec.GenWidth,
                GenHeight = spec.GenHeight,
                BraidPermille = spec.BraidPermille,
            };
        }

        /// <summary>行から Spec（真実の形）へ戻す。</summary>
        public Sample_StageSpec ToSpec()
        {
            return new Sample_StageSpec(Id, DebugName, DisplayName,
                new UnityEngine.Color(GroundR, GroundG, GroundB), GroundScale,
                EnemyAttackInterval, GeneratorKind, GenWidth, GenHeight, BraidPermille);
        }
    }
}
