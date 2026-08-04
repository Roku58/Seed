using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// Logic が Actor へ渡す1フレームぶんの「意図」。
    /// 「移動（連続値）＋継続フラグ（ガード）＋離散行動の要求1件」の3層構造で、
    /// 行動の種類を増やしても本型は不変（RequestedAction のキーが増えるだけ）。
    /// </summary>
    public readonly struct CharacterIntent
    {
        /// <summary>「何もしない」意図。</summary>
        public static readonly CharacterIntent None = default;

        /// <summary>移動したいワールド方向（大きさ0〜1。0なら停止したい）。</summary>
        public readonly Vector3 MoveDirection;

        /// <summary>ガードを構え続けたいか（継続入力）。</summary>
        public readonly bool GuardHeld;

        /// <summary>要求する離散行動（None なら要求なし）。</summary>
        public readonly BehaviorKey RequestedAction;

        /// <summary>行動に添える荷物（技IDなど。AttackRequested.MoveId へ素通しされる）。</summary>
        public readonly int ActionPayload;

        /// <summary>CharacterIntent を生成する。</summary>
        public CharacterIntent(Vector3 moveDirection, bool guardHeld, BehaviorKey requestedAction, int actionPayload)
        {
            MoveDirection = moveDirection;
            GuardHeld = guardHeld;
            RequestedAction = requestedAction;
            ActionPayload = actionPayload;
        }
    }
}
