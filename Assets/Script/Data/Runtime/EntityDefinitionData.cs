using System;

namespace Seed.Data
{
    /// <summary>
    /// 純C#の定義DTO基底（ScriptableObject を使わない側の受け皿）。
    ///
    /// [役割] JSON / 手書きコード / 将来のCSVローダなど、UnityEngine の資産機構を通さない
    /// 経路で作られる定義の共通部分。ゲームごとの定義は本クラスを継承して
    /// 数値フィールドを足すだけでよい（例: <c>class WeaponData : EntityDefinitionData { public int attack; }</c>）。
    ///
    /// [なぜ ScriptableObject 版と2本立てなのか]
    /// SO 版（EntityDefinitionAsset）は「企画がInspectorで並べる」ための形、
    /// 本クラスは「テキストデータ・テストコードから作る」ための形。
    /// どちらも IEntityDefinition しか外に見せないので、Seed.Core から見れば区別がつかない
    /// ＝入口を増やしても Core は変わらない、という層構造を保てる。
    ///
    /// [フィールド命名の例外規約] 公開フィールドを小文字始まりにしている。
    /// JsonUtility は「フィールド名＝JSONのキー名」で対応付けるため、
    /// <c>_id</c> や <c>Id</c> にすると JSON 側が <c>"_id"</c> / <c>"Id"</c> になって企画が書きにくい。
    /// JSONの可読性を優先した本基盤唯一の命名例外であり、他の場所へ真似して広げないこと。
    /// </summary>
    [Serializable]
    public class EntityDefinitionData : IEntityDefinition
    {
        /// <summary>安定ID（1以上）。JSONキーは "id"。</summary>
        public int id;

        /// <summary>ログ・例外用の識別名。JSONキーは "debugName"。</summary>
        public string debugName;

        /// <summary>デシリアライザ用の既定コンストラクタ（JsonUtility は引数付きctorを呼べない）。</summary>
        public EntityDefinitionData()
        {
        }

        /// <summary>コード上で直接組み立てる用（テスト・プロトタイプ）。</summary>
        public EntityDefinitionData(int id, string debugName)
        {
            this.id = id;
            this.debugName = debugName;
        }

        /// <summary>安定ID（IEntityDefinition 実装。フィールド id をそのまま公開する）。</summary>
        public int Id => id;

        /// <summary>識別名（IEntityDefinition 実装。未設定なら型名で代替してログの手掛かりを残す）。</summary>
        public string DebugName => string.IsNullOrEmpty(debugName) ? GetType().Name : debugName;

        /// <summary>ログ用の短い表記（例: "WeaponData#10 爆鱗の剣"）。</summary>
        public override string ToString()
        {
            return string.Concat(GetType().Name, "#", id.ToString(), " ", DebugName);
        }
    }
}
