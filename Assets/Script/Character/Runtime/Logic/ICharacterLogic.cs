namespace Seed.Character
{
    /// <summary>
    /// 「次に何をしたいか」を決める頭脳の差し込み口。
    /// プレイヤー直接操作なら ManualLogic、AI制御なら思考ルーチンの実装をここに挿す。
    /// 思考ルーチンの中身（いつ攻撃するか等）はゲームの方針なのでアプリ側に置き、
    /// 基盤は無方針の器（本インターフェースと Manual/Null）だけを提供する。
    ///
    /// Hub には依存しない——意図を返すだけの純粋な関数であること。
    /// 外界（他キャラの位置など）は実装のコンストラクタで ICharacterQuery を注入して読む。
    /// </summary>
    public interface ICharacterLogic
    {
        /// <summary>自分の今（LogicFrame）を見て、1フレームぶんの意図を返す。</summary>
        CharacterIntent Think(in LogicFrame frame);
    }
}
