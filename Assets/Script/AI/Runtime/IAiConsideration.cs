using Seed.Character;

namespace Seed.AI
{
    /// <summary>
    /// 思考の1候補（ユーティリティAIの評価単位。「追う」「攻撃する」「逃げる」…候補の数だけ実装する）。
    ///
    /// Score で「今それをやりたい度」を返し、AiBrain が最高得点の候補の Act を採用する。
    /// 0以下は不参加。得点の重み付けそのもの（距離が近いほど攻撃したい等）が
    /// ゲームの個性＝方針なので、具体的な Consideration はアプリ側に置く
    /// （Behavior と同じ「数だけ用意する」拡張点。基盤は器だけを提供する）。
    ///
    /// 規約: Score は世界を変更しない（読み取りのみ）。記憶への書き込みは Act で行う。
    /// </summary>
    public interface IAiConsideration
    {
        /// <summary>今この行動をやりたい度（0以下=不参加）。</summary>
        float Score(in AiContext context);

        /// <summary>採用されたときの意図（入力と同じ語彙＝CharacterIntent）を返す。</summary>
        CharacterIntent Act(in AiContext context);
    }
}
