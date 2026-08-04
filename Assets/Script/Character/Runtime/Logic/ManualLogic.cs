using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// プレイヤー直接操作の Logic。
    /// 入力デバイスの読み取り（UnityEngine.Input 等）はアプリの合成ルートの仕事で、
    /// 本クラスは「入力の一時保管 → 意図への変換」だけを担う。
    /// このため入力実装に依存せず、テストでは Set系を直接呼んで検証できる。
    /// </summary>
    public sealed class ManualLogic : ICharacterLogic
    {
        /// <summary>移動方向（毎フレーム上書き）。</summary>
        private Vector3 _move;

        /// <summary>ガード継続入力。</summary>
        private bool _guardHeld;

        /// <summary>予約中の離散行動（一度 Think で返したら消費する）。</summary>
        private BehaviorKey _bufferedAction = BehaviorKey.None;

        /// <summary>予約中の行動の荷物（技IDなど）。</summary>
        private int _bufferedPayload;

        /// <summary>移動方向を設定する（大きさ1超は正規化。毎フレーム上書きする）。</summary>
        public void SetMove(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude > 1f)
            {
                worldDirection.Normalize();
            }
            _move = worldDirection;
        }

        /// <summary>ガードの継続入力を設定する。</summary>
        public void SetGuard(bool held)
        {
            _guardHeld = held;
        }

        /// <summary>離散行動を1回ぶん予約する（未消費のうちに再予約したら後勝ち）。</summary>
        public void RequestAction(BehaviorKey behavior, int payload = 0)
        {
            _bufferedAction = behavior;
            _bufferedPayload = payload;
        }

        /// <summary>保管中の入力を意図へ変換する。予約行動は一度返したら消費する。</summary>
        public CharacterIntent Think(in LogicFrame frame)
        {
            var intent = new CharacterIntent(_move, _guardHeld, _bufferedAction, _bufferedPayload);
            _bufferedAction = BehaviorKey.None;
            _bufferedPayload = 0;
            return intent;
        }
    }
}
