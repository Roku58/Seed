using System.IO;

namespace Seed.Core.Samples.ActionBattle
{
    /// <summary>
    /// 【サンプル】入力のバイト列変換（★パターン: IInputCodec ＝ リプレイ保存の完成）。
    /// InputJournalCodec と組み合わせると、リプレイを byte[]（→ファイル/通信）に落とせる。
    /// フィールドを固定順で全て読み書きすること。形式を変えたら InputJournal.Version を上げる。
    /// </summary>
    public sealed class Sample_ActionInputCodec : IInputCodec<Sample_ActionInput>
    {
        /// <summary>入力をバイト列へ書き込む。</summary>
        public void Write(BinaryWriter writer, in Sample_ActionInput input)
        {
            writer.Write((byte)input.Kind);
            writer.Write(input.MoveId);
            writer.Write(input.PartId);
            writer.Write(input.Flag);
            writer.Write(input.Value);
            writer.Write(input.Value2);
        }

        /// <summary>バイト列から入力を読み取る。</summary>
        public Sample_ActionInput Read(BinaryReader reader)
        {
            var kind = (Sample_ActionInputKind)reader.ReadByte();
            var moveId = reader.ReadInt32();
            var partId = reader.ReadInt32();
            var flag = reader.ReadBoolean();
            var value = reader.ReadInt32();
            var value2 = reader.ReadInt32();

            switch (kind)
            {
                case Sample_ActionInputKind.AdvanceTime:
                    /// <summary>時間を進める（ミリ秒）。</summary>
                    return Sample_ActionInput.AdvanceTime(value);
                case Sample_ActionInputKind.UseDemonDrug:
                    /// <summary>鬼人薬を使用する（Extendポリシーで延長）。</summary>
                    return Sample_ActionInput.UseDemonDrug(value, value2);
                case Sample_ActionInputKind.HunterAttack:
                    /// <summary>ハンターの攻撃（発動判定→ヒット解決）を実行する。</summary>
                    return Sample_ActionInput.HunterAttack(moveId, partId);
                default:
                    /// <summary>モンスターの攻撃（同じセクションを再利用）を実行する。</summary>
                    return Sample_ActionInput.MonsterAttack(moveId, flag);
            }
        }
    }
}
