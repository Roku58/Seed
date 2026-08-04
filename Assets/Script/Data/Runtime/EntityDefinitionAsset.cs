using UnityEngine;

namespace Seed.Data
{
    /// <summary>
    /// ScriptableObject の定義アセット基底（企画が Inspector で並べる側の入口）。
    ///
    /// [2本立ての片割れ]
    /// - EntityDefinitionData … JSON・コード・テストから作る純C#のDTO
    /// - 本クラス             … .asset として保存し Inspector で編集する形
    /// どちらも外へは IEntityDefinition しか見せないため、MasterDataSet / MasterDataLoader /
    /// Seed.Core からは区別がつかない＝入口を増やしても中身は一切変わらない。
    ///
    /// [使い方] ゲームごとの定義は本クラスを継承してフィールドを足し、
    /// [CreateAssetMenu] を付けてアセット化する。ロード時は
    /// 収集したアセット群を MasterDataSet.Add して ValidateGlobalIdUniqueness →
    /// MasterDataLoader.RegisterAll の流れ（純C#DTOと完全に同じ）。
    /// </summary>
    public abstract class EntityDefinitionAsset : ScriptableObject, IEntityDefinition
    {
        /// <summary>安定ID（1以上。レコード・セーブ・リプレイにそのまま載る値）。</summary>
        [SerializeField] private int _id;

        /// <summary>ログ・例外用の識別名（空ならアセット名で代替）。</summary>
        [SerializeField] private string _debugName;

        /// <summary>安定ID。</summary>
        public int Id => _id;

        /// <summary>識別名（未設定ならアセット名）。</summary>
        public string DebugName => string.IsNullOrEmpty(_debugName) ? name : _debugName;

        /// <summary>ログ用の短い表記。</summary>
        public override string ToString()
        {
            return string.Concat(GetType().Name, "#", _id.ToString(), " ", DebugName);
        }
    }
}
