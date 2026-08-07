using Seed.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】アセット不要のキーボード入力リーダー（Input System 直読み）。
    ///
    /// 「空のGameObjectにアタッチしてPlayするだけで動く」デモの都合上、
    /// InputActionAsset の割り当てを要求しない最小実装を用意している。
    /// 実プロジェクトでは InputSystemReader（.inputactions 駆動・リバインド可能）を使うこと。
    /// 割り当て: WASD=移動 / [1]=Attack / [2]=Interact / [3]=Submit /
    /// [G]=Guard / [T]=Next（Actor切替に流用）/ [B]=Cancel（戻る）/ [P]=Previous（ポーズに流用）/ [O]=Jump（自動操縦切替に流用）。
    /// ボタンの「意味」はフェーズごとに変わる（ホームの[1]は出撃、戦闘の[1]は攻撃）
    /// ——意味づけは各フェーズの仕事で、リーダーは状態を写すだけ。
    /// </summary>
    public sealed class Sample_KeyboardReader : IInputReader
    {
        /// <summary>現在のキーボード状態をスナップショットへ写す。</summary>
        public InputSnapshot Read()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return InputSnapshot.Empty;
            }

            var move = Vector2.zero;
            if (keyboard.wKey.isPressed) { move.y += 1f; }
            if (keyboard.sKey.isPressed) { move.y -= 1f; }
            if (keyboard.dKey.isPressed) { move.x += 1f; }
            if (keyboard.aKey.isPressed) { move.x -= 1f; }

            ulong pressed = 0;
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Attack, keyboard.digit1Key.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Interact, keyboard.digit2Key.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Submit, keyboard.digit3Key.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, Sample_ActionIds.Slot4, keyboard.digit4Key.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, Sample_ActionIds.Slot5, keyboard.digit5Key.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, Sample_ActionIds.CycleView, keyboard.cKey.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Guard, keyboard.gKey.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Next, keyboard.tKey.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Cancel, keyboard.bKey.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Previous, keyboard.pKey.isPressed);
            pressed = InputSnapshot.SetPressed(pressed, ActionId.Jump, keyboard.oKey.isPressed);

            return new InputSnapshot(move, Vector2.zero, pressed);
        }
    }
}
