using System;

namespace Seed.Input
{
    /// <summary>
    /// 入力アクションの種別ID。
    /// enum にしない理由: アクションは「ゲームごとに増える語彙」であり、
    /// アプリ側が基盤（Seed.Input）に手を入れず独自アクションを追加できるようにするため
    /// （Seed.Hub.Contracts.ScreenId / Seed.Character.BehaviorKey と同じ流儀の値型ID）。
    ///
    /// 割り当て規約:
    /// - 0 … <see cref="None"/>（未指定）。ビットマスクの都合で「ボタンとして使えない」値でもある
    /// - 1〜99 … 本基盤が予約する標準アクション
    /// - 100以降 … アプリ独自アクション
    ///
    /// ただし <see cref="InputSnapshot"/> は押下状態を 64bit のビットマスクで持つため、
    /// 「押しているか」を写せるのは 1〜<see cref="InputSnapshot.MaxButtonActionValue"/> のIDだけ。
    /// よってアプリ独自の"ボタン"は 100 以降ではなく空き番の 32〜63 を使い、
    /// 100 以降は「ボタン以外の入力（軸・ジェスチャ等）を将来アプリ側の器で扱う」ための番号帯とする。
    /// この非対称さは「1フレームぶんの入力を確保ゼロの readonly struct で運ぶ」ことの対価で、
    /// 辞書に置き換えるより毎フレームのコストが読みやすいと判断した。
    /// </summary>
    public readonly struct ActionId : IEquatable<ActionId>
    {
        /// <summary>「アクションなし」を表す予約値。ボタンビットには載らない。</summary>
        public static readonly ActionId None = new ActionId(0);

        /// <summary>攻撃（既定アセットの Player/Attack）。</summary>
        public static readonly ActionId Attack = new ActionId(1);

        /// <summary>
        /// ガード（構え続ける継続系）。
        /// Unity既定の InputSystem_Actions.inputactions には対応アクションが無いため、
        /// 既定の対応表には載せていない。アプリが
        /// <see cref="InputSystemReader.Bind"/> で任意のアクション名へ割り当てる。
        /// </summary>
        public static readonly ActionId Guard = new ActionId(2);

        /// <summary>ジャンプ（既定アセットの Player/Jump）。</summary>
        public static readonly ActionId Jump = new ActionId(3);

        /// <summary>調べる・話す（既定アセットの Player/Interact）。</summary>
        public static readonly ActionId Interact = new ActionId(4);

        /// <summary>取り消し・戻る（既定アセットの UI/Cancel）。</summary>
        public static readonly ActionId Cancel = new ActionId(5);

        /// <summary>決定（既定アセットの UI/Submit）。</summary>
        public static readonly ActionId Submit = new ActionId(6);

        /// <summary>ダッシュ（既定アセットの Player/Sprint）。</summary>
        public static readonly ActionId Sprint = new ActionId(7);

        /// <summary>しゃがみ（既定アセットの Player/Crouch）。</summary>
        public static readonly ActionId Crouch = new ActionId(8);

        /// <summary>前の対象へ（既定アセットの Player/Previous）。</summary>
        public static readonly ActionId Previous = new ActionId(9);

        /// <summary>次の対象へ（既定アセットの Player/Next）。</summary>
        public static readonly ActionId Next = new ActionId(10);

        /// <summary>ID値（1以上が有効。ボタンとして使うなら 1〜63）。</summary>
        public readonly int Value;

        /// <summary>ActionId を生成する。</summary>
        public ActionId(int value)
        {
            Value = value;
        }

        /// <summary>同一アクションかを返す。</summary>
        public bool Equals(ActionId other)
        {
            return Value == other.Value;
        }

        /// <summary>同一アクションかを返す。</summary>
        public override bool Equals(object obj)
        {
            return obj is ActionId other && Equals(other);
        }

        /// <summary>ID値をそのままハッシュにする。</summary>
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>デバッグ表示。</summary>
        public override string ToString()
        {
            return $"Action#{Value}";
        }
    }
}
