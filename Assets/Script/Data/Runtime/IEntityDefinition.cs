namespace Seed.Data
{
    /// <summary>
    /// マスターデータ1件（武器・モンスター・技・部位…）が満たすべき最小契約。
    ///
    /// [なぜ純C#のインターフェースなのか＝層構造の要]
    /// Seed.Core は UnityEngine 非依存（決定性・ヘッドレス実行・EditModeテストの純度維持）。
    /// よって ScriptableObject を Core に持ち込んではならない。
    /// 本基盤 Seed.Data が「SO / JSON → 本契約を実装した純C#の定義 → EntityRegistry / FactoryRegistry」
    /// という一方向の変換を担い、Core 側が触るのは常にこの純C#契約だけになる。
    /// Unity の資産形式（SO・JSON・将来のCSVやアドレサブル）が増えても、
    /// 増えるのは Seed.Data の入口だけで Core は一切変わらない——これが「増やしやすい」の実体。
    ///
    /// [なぜIDをデータ側が明示的に持つのか]
    /// レコード・セーブ・リプレイに載せられるのは EntityRegistry のID（整数）だけである
    /// （オブジェクト参照は直列化できない）。IDをコードの登録順で自動採番すると、
    /// 合成ルートに1行足しただけで保存済みリプレイ・セーブのIDがズレて再生不能になる。
    /// だからIDは「データが持つ値」とし、EntityRegistry.RegisterWithId でその値のまま登録する
    /// （＝登録順依存の根絶。EntityRegistry.RegisterWithId のdocが推奨する運用そのもの）。
    /// </summary>
    public interface IEntityDefinition
    {
        /// <summary>
        /// 定義の安定ID。1以上（0 は EntityRegistry.None＝「対象なし」の予約値なので使用不可）。
        /// EntityRegistry と同一のID体系であり、ここに書いた値がそのままレコードに載る。
        /// </summary>
        int Id { get; }

        /// <summary>
        /// ログ・例外メッセージ・エディタ表示用の識別名。
        /// ゲーム内に出す表示文言（ローカライズ対象）ではない点に注意——
        /// 表示文言は各ゲームの定義型が自前のフィールドとして持つこと。
        /// </summary>
        string DebugName { get; }
    }
}
