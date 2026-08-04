using System;
using Seed.Core;

namespace Seed.App
{
    /// <summary>
    /// 決定的ロジックへの入力の一本道（★規約: 記録してから実行）。
    ///
    /// GameCore の看板（決定的リプレイ→将来のロックステップ通信・サーバ検証）は
    /// 「全入力がジャーナルを通る」ことで初めて成立する。旧構成の Bridge は
    /// セクションを直叩きしており、統合経路のプレイだけが記録から漏れていた。
    ///
    /// 本クラスの規約:
    /// - Hub メッセージ由来だろうが時間前進だろうが、ロジックを動かす入力は
    ///   すべて Submit を通す（Bridge はロジックの入力型を作るだけで、セクションを叩かない）
    /// - Submit は必ず「ジャーナルへ記録 → 実行」の順（クラッシュしても記録が先に残る）
    /// リプレイ再生・リモート入力（通信対戦）は、Submit の呼び出し元を
    /// 差し替えるだけで同じ経路に乗る。
    /// </summary>
    public sealed class LogicInputFunnel<TInput> where TInput : struct
    {
        /// <summary>入力1件の実行本体（通常はドライバの Execute）。</summary>
        public delegate void ExecuteHandler(in TInput input);

        /// <summary>記録先のジャーナル。</summary>
        private readonly InputJournal<TInput> _journal;

        /// <summary>実行本体。</summary>
        private readonly ExecuteHandler _execute;

        /// <summary>現在時刻の取得（ジャーナルの時刻印用）。</summary>
        private readonly Func<long> _nowMs;

        /// <summary>LogicInputFunnel を生成する。</summary>
        public LogicInputFunnel(InputJournal<TInput> journal, ExecuteHandler execute, Func<long> nowMs)
        {
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
        }

        /// <summary>記録済みの入力件数。</summary>
        public int Count => _journal.Count;

        /// <summary>入力を1件、記録してから実行する（ロジックを動かす唯一の道）。</summary>
        public void Submit(in TInput input)
        {
            _journal.Record(_nowMs(), in input);
            _execute(in input);
        }
    }
}
