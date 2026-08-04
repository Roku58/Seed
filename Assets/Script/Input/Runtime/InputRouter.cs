using System;
using UnityEngine;

namespace Seed.Input
{
    /// <summary>
    /// 入力の中核。<see cref="IInputReader"/> から毎フレーム読み、前フレームとの差分で
    /// 「押した瞬間 / 離した瞬間」を出す。読み取り（デバイス依存）と意味づけ（アプリの方針）の
    /// あいだに立ち、状態と変化の両方を同期的に問い合わせられる唯一の窓口になる。
    ///
    /// エッジ検出を基盤側に置く理由:
    /// これまで各デモが「前フレームの押下値をフィールドに覚えて今フレームと比較する」コードを
    /// 別々に書いており、同じ仕組みを何度も再発明していた。しかも比較対象を1か所忘れると
    /// 「押しっぱなしで攻撃が連射される」種の不具合になり、原因が入力の外側に見えてしまう。
    /// 差分の持ち主をここ1つに固定すれば、アプリは
    /// <see cref="WasPressedThisFrame"/> を読むだけで済み、前フレーム値を持たなくてよい。
    ///
    /// MonoBehaviour ではなく純C#にした理由:
    /// - プロジェクト規約「Tick は合成ルートから1本」に合わせる。<see cref="Tick"/> は
    ///   合成ルートの Update が1フレームにちょうど1回呼ぶ（実行順を Script Execution Order に頼らない）
    /// - NUnit EditMode から偽の読み取り実装を挿して直接検証できる
    ///
    /// 本クラスは Hub へ何も発行しない（基盤から Hub への発行はゼロ、の規約）。
    /// 「Attack が押された → AttackRequested を発行する」といった翻訳はアプリの方針が担う。
    /// </summary>
    public sealed class InputRouter
    {
        /// <summary>入力の読み取り実装（差し替え可能）。</summary>
        private readonly IInputReader _reader;

        /// <summary>今フレームの写し。</summary>
        private InputSnapshot _current;

        /// <summary>前フレームの写し（差分の基準。これがあるからアプリ側は履歴を持たなくてよい）。</summary>
        private InputSnapshot _previous;

        /// <summary>InputRouter を生成する。読み取り実装は必須（配線漏れは即座に気付きたい）。</summary>
        public InputRouter(IInputReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _current = InputSnapshot.Empty;
            _previous = InputSnapshot.Empty;
        }

        /// <summary>今フレームの入力の写し（連続値をまとめて渡したいときに使う）。</summary>
        public InputSnapshot Current => _current;

        /// <summary>前フレームの入力の写し（デバッグ・独自の差分判定用）。</summary>
        public InputSnapshot Previous => _previous;

        /// <summary>今フレームの移動入力。</summary>
        public Vector2 Move => _current.Move;

        /// <summary>今フレームの視点入力。</summary>
        public Vector2 Look => _current.Look;

        /// <summary>
        /// 1フレーム進める。今の写しを「前」へ送り、読み取り実装から新しい写しを得る。
        /// 合成ルートが1フレームに1回だけ呼ぶこと——2回呼ぶと押した瞬間が消える（前＝今になる）。
        /// </summary>
        public void Tick()
        {
            _previous = _current;
            _current = _reader.Read();
        }

        /// <summary>指定アクションが今押されているか（継続入力の判定に使う）。</summary>
        public bool IsPressed(ActionId action)
        {
            return _current.IsPressed(action);
        }

        /// <summary>
        /// 指定アクションを今フレームに押し始めたか。
        /// 押し続けている間は false（＝離して押し直すまで再び true にならない）。
        /// </summary>
        public bool WasPressedThisFrame(ActionId action)
        {
            return _current.IsPressed(action) && !_previous.IsPressed(action);
        }

        /// <summary>指定アクションを今フレームに離したか（ため攻撃の解放・ガード終了の検知に使う）。</summary>
        public bool WasReleasedThisFrame(ActionId action)
        {
            return !_current.IsPressed(action) && _previous.IsPressed(action);
        }

        /// <summary>
        /// 入力履歴を今の状態で揃え直す（今を読み直して「前＝今」にする）。
        /// 画面遷移・ポーズ復帰・入力マップ切替の直後に呼ぶ用途。差分の基準を今に置くので、
        /// 「切替前から押されていたボタンが、切替後の最初のフレームで押した瞬間として湧く」
        /// 種の誤検出（＝ポーズ解除と同時に攻撃が出る）を止められる。
        /// </summary>
        public void Reset()
        {
            _current = _reader.Read();
            _previous = _current;
        }
    }
}
