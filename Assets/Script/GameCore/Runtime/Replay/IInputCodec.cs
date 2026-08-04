using System.IO;

namespace Seed.Core
{
    /// <summary>
    /// 入力structのバイト列変換（リプレイ保存用）。
    /// コアは入力の中身を知らないため、変換はゲーム側がこのインターフェースで提供する。
    /// 実装例: Samples/ActionBattle/Sample_ActionInputCodec.cs
    /// </summary>
    public interface IInputCodec<TInput> where TInput : struct
    {
        void Write(BinaryWriter writer, in TInput input);
        TInput Read(BinaryReader reader);
    }
}
