namespace Seed.Character
{
    /// <summary>
    /// プレイヤーユニットの制御。頭脳はマニュアル操作（ManualLogic）前提。
    /// アプリの入力ハンドラは Manual プロパティ経由で SetMove / SetGuard / RequestAction を叩く。
    /// 将来「プレイヤーだけのカメラ連動・入力バッファ拡張」等の置き場もここになる。
    /// </summary>
    public sealed class PlayerController : UnitController
    {
        /// <summary>PlayerController を生成する。</summary>
        public PlayerController(CharacterAgent agent, ManualLogic manual)
            : base(agent, manual)
        {
            Manual = manual;
        }

        /// <summary>入力の接続先（合成ルートの入力ハンドラが叩く）。</summary>
        public ManualLogic Manual { get; }
    }
}
