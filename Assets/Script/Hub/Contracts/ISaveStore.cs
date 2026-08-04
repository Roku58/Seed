namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 「byte[] を安全に永続化する」窓口（ServiceRegistry 経由で借りる）。
    ///
    /// ゲーム状態の直列化（何をbyte[]にするか）は各ゲームの責務のまま、
    /// 「ディスクへ安全に置く」層だけをエンジンが持つ——という役割分担の契約。
    /// 実装（Seed.Persistence の FileSaveStore）は封筒形式（magic/版数/ハッシュ）と
    /// 原子的書き込み（一時ファイル→置換）を標準装備し、
    /// 書き込み中クラッシュや破損データの読み込みからゲームを守る。
    /// リプレイ（InputJournalCodec の byte[]）・セーブデータ・設定の保存先はすべてここ。
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>保存する（失敗時は false。例外は投げない）。</summary>
        bool TrySave(string key, byte[] data);

        /// <summary>読み込む（存在しない・破損している場合は false）。</summary>
        bool TryLoad(string key, out byte[] data);

        /// <summary>存在するか。</summary>
        bool Exists(string key);

        /// <summary>削除する（存在しなければ false）。</summary>
        bool Delete(string key);
    }
}
