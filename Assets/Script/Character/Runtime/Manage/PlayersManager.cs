using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// プレイヤー陣営の管理（FactionId.Players）。
    /// 現状は基底そのまま＋単騎デモ向けの糖衣（Primary）だが、
    /// 「プレイヤーだけの共通処理」（パーティ編成・操作切替など）の置き場として型を分けている。
    /// </summary>
    public sealed class PlayersManager : UnitManager
    {
        /// <summary>PlayersManager を生成する。</summary>
        public PlayersManager(CharacterRegistry registry)
            : base(registry, FactionId.Players)
        {
        }

        /// <summary>最初に追加されたユニット（単騎前提のデモ用の糖衣。空なら null）。</summary>
        public UnitController Primary => Count > 0 ? ControllerAt(0) : null;
    }
}
