using System;
using Seed.Character;

namespace Seed.AI
{
    /// <summary>
    /// 入力のエミュレート（AIの意図を手動入力の置き場へ注入する橋）。
    ///
    /// 「キャラクターの制御はプレイヤーもNPCも共通」の要——
    /// プレイヤーユニットは ManualLogic（手動入力の置き場）で動いているが、
    /// 本クラスを挟めば AIの CharacterIntent をキー入力と同じ形で書き込める。
    /// 自動操縦（オートバトル）・デモプレイ・カットシーン・チュートリアルの誘導が、
    /// Controller / Agent / Behavior を一切変えずに実現できる。
    /// 切るのも一瞬——Drive を呼ぶのをやめれば、次のフレームから人間の入力に戻る。
    /// </summary>
    public sealed class InputEmulator
    {
        /// <summary>注入先（プレイヤーユニットの手動入力の置き場）。</summary>
        private readonly ManualLogic _target;

        /// <summary>InputEmulator を生成する。</summary>
        public InputEmulator(ManualLogic target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        /// <summary>AIの意図を入力として書き込む（毎フレーム、人間の入力処理の代わりに呼ぶ）。</summary>
        public void Drive(in CharacterIntent intent)
        {
            _target.SetMove(intent.MoveDirection);
            _target.SetGuard(intent.GuardHeld);
            if (!intent.RequestedAction.Equals(BehaviorKey.None))
            {
                _target.RequestAction(intent.RequestedAction, intent.ActionPayload);
            }
        }

        /// <summary>入力を離す（自動操縦の解除時に呼ぶ。移動・ガードを止める）。</summary>
        public void ReleaseAll()
        {
            _target.SetMove(UnityEngine.Vector3.zero);
            _target.SetGuard(false);
        }
    }
}
