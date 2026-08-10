using System.IO;
using Seed.App.Master;
using Seed.Data;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// マスターデータのバイナリ入出力（MasterMemory の焼き込みと読み込み）。
    ///
    /// [流れ] 入力（コード直書き・SO・将来はエクセル/CSV）→ <see cref="Bake"/> で
    /// バイナリへ焼く（ビルド工程）→ 実行時は <see cref="LoadCatalog"/> がバイナリを読む。
    /// バイナリが無ければコード直書きカタログへフォールバックするので、
    /// ベイクを一度も実行していない環境でもデモは従来どおり動く。
    ///
    /// [なぜバイナリか] JSON/SO の逐次パースと違い、MasterMemory は読み取り専用
    /// インメモリDBとして数万行でも ms 級でロードでき、インデックス検索と
    /// メモリ効率を併せ持つ。真実（Spec と MasterDataSet の契約）は変えず、
    /// 保存形式だけをこの背面で差し替えている。
    /// </summary>
    public static class Sample_MasterBinary
    {
        /// <summary>バイナリの置き場所（StreamingAssets はビルドにそのまま同梱される）。</summary>
        public static string BinaryPath => Path.Combine(Application.streamingAssetsPath, "master.bytes");

        /// <summary>カタログをバイナリへ焼く（エディタのベイク工程から呼ぶ）。</summary>
        public static byte[] Bake(MasterDataSet catalog)
        {
            var builder = new DatabaseBuilder();

            var units = catalog.GetAll<Sample_UnitSpec>();
            var unitRows = new Sample_UnitRow[units.Count];
            for (var i = 0; i < units.Count; i++)
            {
                unitRows[i] = Sample_UnitRow.From(units[i]);
            }
            builder.Append(unitRows);

            var stages = catalog.GetAll<Sample_StageSpec>();
            var stageRows = new Sample_StageRow[stages.Count];
            for (var i = 0; i < stages.Count; i++)
            {
                stageRows[i] = Sample_StageRow.From(stages[i]);
            }
            builder.Append(stageRows);

            var events = catalog.GetAll<Sample_EventSpec>();
            var eventRows = new Sample_EventRow[events.Count];
            for (var i = 0; i < events.Count; i++)
            {
                eventRows[i] = Sample_EventRow.From(events[i]);
            }
            builder.Append(eventRows);

            return builder.Build();
        }

        /// <summary>バイナリからカタログを復元する（実行時ロード）。</summary>
        public static MasterDataSet Load(byte[] binary)
        {
            var database = new MemoryDatabase(binary);
            var catalog = new MasterDataSet();
            foreach (var row in database.Sample_UnitRowTable.All)
            {
                catalog.Add(row.ToSpec());
            }
            foreach (var row in database.Sample_StageRowTable.All)
            {
                catalog.Add(row.ToSpec());
            }
            foreach (var row in database.Sample_EventRowTable.All)
            {
                catalog.Add(row.ToSpec());
            }
            catalog.ValidateGlobalIdUniqueness();
            return catalog;
        }

        /// <summary>
        /// 実行時のカタログ入手口。ベイク済みバイナリがあればそれを、
        /// 無ければコード直書きカタログを返す（どちらでも同じ MasterDataSet 契約）。
        /// </summary>
        public static MasterDataSet LoadCatalog(
            Seed.Hub.Contracts.CharacterId playerId, Seed.Hub.Contracts.CharacterId enemyId)
        {
            if (File.Exists(BinaryPath))
            {
                var catalog = Load(File.ReadAllBytes(BinaryPath));
                Debug.Log($"[Master] ベイク済みバイナリから読込: {BinaryPath}");
                return catalog;
            }
            return Sample_MasterCatalog.Build(playerId, enemyId);
        }
    }
}
