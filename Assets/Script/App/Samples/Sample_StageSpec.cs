using Seed.Data;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ステージ1面のマスターデータ。
    /// 「ステージを増やす＝この定義を1件足す」の実例（定義ID = StageId.Value）。
    /// デモは地面の色・広さ・敵の攻撃間隔を変えるだけだが、実プロジェクトでは
    /// シーン名（ISceneLoader へ渡す）・出現テーブル・BGM ID などがここに載る。
    /// </summary>
    public sealed class Sample_StageSpec : IEntityDefinition
    {
        /// <summary>定義の安定ID（StageId と同値運用）。</summary>
        public int Id { get; }

        /// <summary>ログ・エディタ表示用の識別名。</summary>
        public string DebugName { get; }

        /// <summary>画面に出すステージ名。</summary>
        public string DisplayName { get; }

        /// <summary>地面の色（ステージの見た目差し替えのデモ）。</summary>
        public Color GroundColor { get; }

        /// <summary>地面の広さ倍率。</summary>
        public float GroundScale { get; }

        /// <summary>このステージの敵の攻撃間隔（秒）。</summary>
        public float EnemyAttackInterval { get; }

        /// <summary>地形の生成方式（0=平地（生成なし） / 1=迷路 / 2=街）。</summary>
        public int GeneratorKind { get; }

        /// <summary>生成グリッドの幅（セル数。奇数推奨）。</summary>
        public int GenWidth { get; }

        /// <summary>生成グリッドの高さ（セル数。奇数推奨）。</summary>
        public int GenHeight { get; }

        /// <summary>迷路の行き止まり貫通率（‰。逃げ道の量）。</summary>
        public int BraidPermille { get; }

        /// <summary>プレイヤーモデルの種類（0=標準: StarterAssets・Controller駆動 / 1=揺れものデモ: UnityChan・Playables）。</summary>
        public int PlayerModelKind { get; }

        /// <summary>Sample_StageSpec を生成する。</summary>
        public Sample_StageSpec(int id, string debugName, string displayName,
            Color groundColor, float groundScale, float enemyAttackInterval,
            int generatorKind = 0, int genWidth = 21, int genHeight = 21, int braidPermille = 0,
            int playerModelKind = 0)
        {
            Id = id;
            DebugName = debugName;
            DisplayName = displayName;
            GroundColor = groundColor;
            GroundScale = groundScale;
            EnemyAttackInterval = enemyAttackInterval;
            GeneratorKind = generatorKind;
            GenWidth = genWidth;
            GenHeight = genHeight;
            BraidPermille = braidPermille;
            PlayerModelKind = playerModelKind;
        }
    }
}
