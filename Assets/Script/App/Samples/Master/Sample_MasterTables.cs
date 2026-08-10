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

        /// <summary>プレイヤーモデルの種類（0=標準 / 1=揺れものデモ）。</summary>
        public int PlayerModelKind { get; set; }

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
                PlayerModelKind = spec.PlayerModelKind,
            };
        }

        /// <summary>行から Spec（真実の形）へ戻す。</summary>
        public Sample_StageSpec ToSpec()
        {
            return new Sample_StageSpec(Id, DebugName, DisplayName,
                new UnityEngine.Color(GroundR, GroundG, GroundB), GroundScale,
                EnemyAttackInterval, GeneratorKind, GenWidth, GenHeight, BraidPermille,
                PlayerModelKind);
        }
    }

    /// <summary>
    /// イベントADV仕様のバイナリ行（MasterMemory テーブル）。
    /// 入れ子リスト（ページ内の演技など）は並行配列のまま持つ——直列化の単純さを優先し、
    /// 台本への組み上げは Spec 側（BuildScript）が担う。
    /// </summary>
    [MemoryTable("event"), MessagePackObject(true)]
    public sealed class Sample_EventRow
    {
        /// <summary>イベントID（主キー）。</summary>
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>識別名。</summary>
        public string DebugName { get; set; }

        /// <summary>窓の名札に出す見出し。</summary>
        public string Title { get; set; }

        /// <summary>ページ本文。</summary>
        public string[] PageTexts { get; set; }

        /// <summary>ページごとの話者キャラID。</summary>
        public int[] PageSpeakers { get; set; }

        /// <summary>ページごとの見せ方（0=窓/1=吹き出し）。</summary>
        public int[] PageStyles { get; set; }

        /// <summary>演技の対象ページ番号。</summary>
        public int[] ActPages { get; set; }

        /// <summary>演技の対象キャラID。</summary>
        public int[] ActActors { get; set; }

        /// <summary>演技の種類（AdvActKind の値）。</summary>
        public int[] ActKinds { get; set; }

        /// <summary>演技の文字列パラメータ。</summary>
        public string[] ActKeys { get; set; }

        /// <summary>演技の補助文字列。</summary>
        public string[] ActSubKeys { get; set; }

        /// <summary>演技の位置 X。</summary>
        public float[] ActX { get; set; }

        /// <summary>演技の位置 Y。</summary>
        public float[] ActY { get; set; }

        /// <summary>演技の位置 Z。</summary>
        public float[] ActZ { get; set; }

        /// <summary>演技の所要秒数。</summary>
        public float[] ActSeconds { get; set; }

        /// <summary>演技の寿命（AdvActLife の値）。</summary>
        public int[] ActLives { get; set; }

        /// <summary>登場キャラのID。</summary>
        public int[] ActorIds { get; set; }

        /// <summary>登場キャラの表示名。</summary>
        public string[] ActorNames { get; set; }

        /// <summary>登場キャラのプレハブ。</summary>
        public string[] ActorPrefabs { get; set; }

        /// <summary>登場キャラのコントローラ。</summary>
        public string[] ActorControllers { get; set; }

        /// <summary>初期位置 X。</summary>
        public float[] ActorX { get; set; }

        /// <summary>初期位置 Y。</summary>
        public float[] ActorY { get; set; }

        /// <summary>初期位置 Z。</summary>
        public float[] ActorZ { get; set; }

        /// <summary>吹き出しオフセット X。</summary>
        public float[] BubbleX { get; set; }

        /// <summary>吹き出しオフセット Y。</summary>
        public float[] BubbleY { get; set; }

        /// <summary>吹き出しオフセット Z。</summary>
        public float[] BubbleZ { get; set; }

        /// <summary>選択肢のラベル。</summary>
        public string[] ChoiceLabels { get; set; }

        /// <summary>選択肢ごとの結果（次イベントID）。</summary>
        public int[] ChoiceResults { get; set; }

        /// <summary>強制イベント読了後の遷移先。</summary>
        public int NextEventId { get; set; }

        /// <summary>背景プレハブのキー。</summary>
        public string BackgroundPrefabKey { get; set; }

        /// <summary>文字送り速度（文字/秒）。</summary>
        public float CharsPerSecond { get; set; }

        /// <summary>舞台モード（0=3D / 1=2D）。</summary>
        public int StageMode { get; set; }

        /// <summary>UIガワのプレハブキー。</summary>
        public string UiPrefabKey { get; set; }

        /// <summary>再生中に他の入力を停止するか。</summary>
        public bool LockOtherInput { get; set; }

        /// <summary>切替用カメラのID。</summary>
        public int[] CamIds { get; set; }

        /// <summary>切替用カメラの位置 X。</summary>
        public float[] CamX { get; set; }

        /// <summary>切替用カメラの位置 Y。</summary>
        public float[] CamY { get; set; }

        /// <summary>切替用カメラの位置 Z。</summary>
        public float[] CamZ { get; set; }

        /// <summary>切替用カメラの注視先キャラID。</summary>
        public int[] CamLookActors { get; set; }

        /// <summary>Spec（真実の形）から行を作る。</summary>
        public static Sample_EventRow From(Sample_EventSpec spec)
        {
            return new Sample_EventRow
            {
                Id = spec.Id,
                DebugName = spec.DebugName,
                Title = spec.Title,
                PageTexts = spec.PageTexts,
                PageSpeakers = spec.PageSpeakers,
                PageStyles = spec.PageStyles,
                ActPages = spec.ActPages,
                ActActors = spec.ActActors,
                ActKinds = spec.ActKinds,
                ActKeys = spec.ActKeys,
                ActSubKeys = spec.ActSubKeys,
                ActX = spec.ActX,
                ActY = spec.ActY,
                ActZ = spec.ActZ,
                ActSeconds = spec.ActSeconds,
                ActLives = spec.ActLives,
                ActorIds = spec.ActorIds,
                ActorNames = spec.ActorNames,
                ActorPrefabs = spec.ActorPrefabs,
                ActorControllers = spec.ActorControllers,
                ActorX = spec.ActorX,
                ActorY = spec.ActorY,
                ActorZ = spec.ActorZ,
                BubbleX = spec.BubbleX,
                BubbleY = spec.BubbleY,
                BubbleZ = spec.BubbleZ,
                ChoiceLabels = spec.ChoiceLabels,
                ChoiceResults = spec.ChoiceResults,
                NextEventId = spec.NextEventId,
                BackgroundPrefabKey = spec.BackgroundPrefabKey,
                CharsPerSecond = spec.CharsPerSecond,
                StageMode = spec.StageMode,
                UiPrefabKey = spec.UiPrefabKey,
                LockOtherInput = spec.LockOtherInput,
                CamIds = spec.CamIds,
                CamX = spec.CamX,
                CamY = spec.CamY,
                CamZ = spec.CamZ,
                CamLookActors = spec.CamLookActors,
            };
        }

        /// <summary>行から Spec（真実の形）へ戻す。</summary>
        public Sample_EventSpec ToSpec()
        {
            return new Sample_EventSpec(Id, DebugName, Title,
                PageTexts, PageSpeakers, PageStyles,
                ActPages, ActActors, ActKinds, ActKeys, ActSubKeys,
                ActX, ActY, ActZ, ActSeconds, ActLives,
                ActorIds, ActorNames, ActorPrefabs, ActorControllers,
                ActorX, ActorY, ActorZ, BubbleX, BubbleY, BubbleZ,
                ChoiceLabels, ChoiceResults,
                NextEventId, BackgroundPrefabKey, CharsPerSecond,
                StageMode, UiPrefabKey, LockOtherInput,
                CamIds, CamX, CamY, CamZ, CamLookActors);
        }
    }
}
