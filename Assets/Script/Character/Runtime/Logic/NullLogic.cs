namespace Seed.Character
{
    /// <summary>
    /// 常に「何もしない」を返す Logic。
    /// 演出専用の置物キャラや、死亡後の頭脳差し替え先として使う。
    /// </summary>
    public sealed class NullLogic : ICharacterLogic
    {
        /// <summary>共有インスタンス（状態を持たないため使い回せる）。</summary>
        public static readonly NullLogic Instance = new NullLogic();

        /// <summary>常に None。</summary>
        public CharacterIntent Think(in LogicFrame frame)
        {
            return CharacterIntent.None;
        }
    }
}
