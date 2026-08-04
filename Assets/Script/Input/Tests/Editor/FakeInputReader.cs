// ============================================================================
// Seed.Input のテストダブル。
// 実デバイスも Unity の実行ループも無しに、任意のスナップショットを InputRouter へ流す。
// ============================================================================

using UnityEngine;

namespace Seed.Input.Tests
{
    /// <summary>
    /// スナップショットを外から差し込める <see cref="IInputReader"/> 実装（テスト用）。
    /// 「読み取りを抽象に切った」設計の実利がここに出る: 入力の抽象が1メソッドなので、
    /// テストダブルはフィールド1つと Read() だけで足りる。
    /// </summary>
    internal sealed class FakeInputReader : IInputReader
    {
        /// <summary>次の <see cref="Read"/> が返す写し。</summary>
        private InputSnapshot _next = InputSnapshot.Empty;

        /// <summary>Read が呼ばれた回数（合成ルートが1フレーム1回だけ読むことの検証に使う）。</summary>
        public int ReadCount { get; private set; }

        /// <summary>移動入力を設定する。</summary>
        public void SetMove(Vector2 move)
        {
            _next = new InputSnapshot(move, _next.Look, _next.Pressed);
        }

        /// <summary>視点入力を設定する。</summary>
        public void SetLook(Vector2 look)
        {
            _next = new InputSnapshot(_next.Move, look, _next.Pressed);
        }

        /// <summary>指定アクションの押下状態を設定する（押しっぱなしは呼ばずに維持される）。</summary>
        public void SetPressed(ActionId action, bool isPressed)
        {
            _next = new InputSnapshot(
                _next.Move,
                _next.Look,
                InputSnapshot.SetPressed(_next.Pressed, action, isPressed));
        }

        /// <summary>写し全体を差し替える。</summary>
        public void SetSnapshot(InputSnapshot snapshot)
        {
            _next = snapshot;
        }

        /// <summary>設定済みの写しをそのまま返す。</summary>
        public InputSnapshot Read()
        {
            ReadCount++;
            return _next;
        }
    }
}
