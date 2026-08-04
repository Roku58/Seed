namespace Seed.Hub.Contracts
{
    /// <summary>
    /// キャラクターの「今」を同期的に問い合わせる窓口（ServiceRegistry 経由で借りる）。
    /// 毎フレームの連続値（位置など）はメッセージに流さず、このインターフェースで読む。
    /// 実装はキャラクター基盤が提供し、他基盤は実装型を知らない。
    ///
    /// 座標は契約層専用の <see cref="HubVector3"/>（純C#）で受け渡す
    /// ——契約層をエンジン非依存に保ち、ヘッドレス検証・エンジン外ツールでも使えるようにするため。
    /// </summary>
    public interface ICharacterQuery
    {
        /// <summary>該当キャラクターが存在し生存しているか。</summary>
        bool IsAlive(CharacterId id);

        /// <summary>位置の取得を試みる（存在しなければ false）。</summary>
        bool TryGetPosition(CharacterId id, out HubVector3 position);
    }

    /// <summary>
    /// 陣営・生存で絞ってキャラクターを列挙する窓口（ターゲット選択AIの土台）。
    /// キャラクター基盤が提供する。列挙はアロケーションを避けるため呼び出し側のバッファへ詰める。
    /// </summary>
    public interface ICharacterRoster
    {
        /// <summary>在籍数。</summary>
        int Count { get; }

        /// <summary>
        /// 条件に合うキャラクターIDを buffer へ詰め、詰めた件数を返す。
        /// faction に <see cref="FactionId.Any"/> を渡すと陣営を問わない。
        /// </summary>
        int Query(FactionId faction, bool aliveOnly, CharacterId[] buffer);

        /// <summary>該当キャラクターの陣営（不在なら <see cref="FactionId.None"/>）。</summary>
        FactionId GetFaction(CharacterId id);
    }
}
