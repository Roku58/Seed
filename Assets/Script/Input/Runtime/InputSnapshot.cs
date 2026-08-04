using UnityEngine;

namespace Seed.Input
{
    /// <summary>
    /// 1フレームぶんの入力の写し（生の状態のみ。意味づけは持たない）。
    ///
    /// 「移動」「視点」は連続値なので素直に <see cref="Vector2"/> で持つ。
    /// 一方ボタンは可変個で、readonly struct に配列や辞書を持たせると
    /// 毎フレームの確保とヌル分岐が増える。そこで押下状態は 64bit のビットマスク
    /// （<see cref="Pressed"/>）で表し、<see cref="ActionId"/> の値をそのままビット位置に対応させる。
    ///
    /// 上限と理由:
    /// - ビット0は <see cref="ActionId.None"/> に対応するため使わない
    /// - よってボタンとして写せるのは ID 1〜<see cref="MaxButtonActionValue"/> の 63 種
    /// - 範囲外のIDは「押されていない」として扱う（例外を投げない。入力は落ちても
    ///   ゲームは続けられるべきで、配線ミスは警告で気付かせる方が実用的）
    ///
    /// この型は「読み取り結果の運搬」だけを担い、前フレームとの比較（押した瞬間の判定）は
    /// <see cref="InputRouter"/> の仕事。差分の持ち主を1か所に寄せることで、
    /// スナップショットは常に「今この瞬間」だけを意味する単純な値でいられる。
    /// </summary>
    public readonly struct InputSnapshot
    {
        /// <summary>ボタンとして押下状態を写せる <see cref="ActionId"/> の最大値（ulong の 63bit ぶん）。</summary>
        public const int MaxButtonActionValue = 63;

        /// <summary>何も入力されていない写し（初期値・リセット時の基準）。</summary>
        public static readonly InputSnapshot Empty = new InputSnapshot(Vector2.zero, Vector2.zero, 0UL);

        /// <summary>移動入力（-1〜1 の2軸。デッドゾーン等の処理はアクション側の Processor に任せる）。</summary>
        public readonly Vector2 Move;

        /// <summary>視点入力（マウス差分やスティック傾き。単位はアセットの設定次第）。</summary>
        public readonly Vector2 Look;

        /// <summary>押下状態のビットマスク（ビットn = ActionId n が押されている）。</summary>
        public readonly ulong Pressed;

        /// <summary>InputSnapshot を生成する。</summary>
        public InputSnapshot(Vector2 move, Vector2 look, ulong pressed)
        {
            Move = move;
            Look = look;
            Pressed = pressed;
        }

        /// <summary>指定アクションが今押されているかを返す（範囲外IDは常に false）。</summary>
        public bool IsPressed(ActionId action)
        {
            var mask = MaskOf(action);
            return mask != 0UL && (Pressed & mask) != 0UL;
        }

        /// <summary>
        /// アクションIDに対応するビットマスクを返す。
        /// ボタンとして使えない値（0以下・64以上）は 0 を返し、呼び側で「押されていない」に畳める。
        /// </summary>
        public static ulong MaskOf(ActionId action)
        {
            var value = action.Value;
            if (value <= 0 || value > MaxButtonActionValue)
            {
                return 0UL;
            }
            return 1UL << value;
        }

        /// <summary>
        /// ビットマスクの1ビットを立てる/落として返す（マスク組み立ての補助）。
        /// 読み取り実装とテストダブルの双方がビット演算を書き写さずに済むよう、ここに1つだけ置く。
        /// </summary>
        public static ulong SetPressed(ulong pressed, ActionId action, bool isPressed)
        {
            var mask = MaskOf(action);
            if (mask == 0UL)
            {
                return pressed;
            }
            return isPressed ? (pressed | mask) : (pressed & ~mask);
        }

        /// <summary>デバッグ表示（マスクは16進で見た方が読みやすい）。</summary>
        public override string ToString()
        {
            return $"Input(Move={Move}, Look={Look}, Pressed=0x{Pressed:X})";
        }
    }
}
