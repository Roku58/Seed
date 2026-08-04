using System;
using System.Collections.Generic;
using Seed.Core;
using Seed.Core.Presenter;
using Seed.Hub;

namespace Seed.App
{
    /// <summary>
    /// レコード→Hubメッセージの宣言的な翻訳表（Bridge の Core→Hub 方向の共通基盤）。
    ///
    /// 旧構成の問題: 翻訳が if の羅列（ホワイトリスト）で、対応漏れのレコード種が
    /// **黙って捨てられ**、ゲームを載せ替えるたびに翻訳ロジック全体を書き直していた。
    ///
    /// 本クラスの規約:
    /// - 翻訳する種は Map で、翻訳しないと決めた種は MapIgnore で、**全種を明示**する
    /// - 表に無い種が流れてきたら Unmapped フックへ通知する（既定は無いので
    ///   合成ルートが必ず配線する。デバッグビルドで警告を出す等）
    /// ゲーム差し替え時に書き直すのは「この表の中身」だけになる。
    /// カーソル管理・巻き戻し追従は RecordDrain に委譲する。
    /// </summary>
    public sealed class RecordHubTranslator<TRecord, TKind>
        where TRecord : struct
        where TKind : struct, Enum
    {
        /// <summary>翻訳1件の本体（メッセージの生成と発行までを行う）。</summary>
        public delegate void TranslateHandler(in TRecord record);

        /// <summary>レコードから種を取り出す関数。</summary>
        private readonly Func<TRecord, TKind> _kindOf;

        /// <summary>種→翻訳の対応表（null は「明示的に無視」）。</summary>
        private readonly Dictionary<TKind, TranslateHandler> _map =
            new Dictionary<TKind, TranslateHandler>();

        /// <summary>新着の汲み上げ役（カーソル・巻き戻し追従はこちらの責務）。</summary>
        private readonly RecordDrain<TRecord> _drain;

        /// <summary>RecordHubTranslator を生成する。</summary>
        public RecordHubTranslator(Func<TRecord, TKind> kindOf)
        {
            _kindOf = kindOf ?? throw new ArgumentNullException(nameof(kindOf));
            _drain = new RecordDrain<TRecord>(Dispatch);
        }

        /// <summary>表に無い種が流れてきたときの通知先（合成ルートが必ず配線すること）。</summary>
        public event Action<TKind> Unmapped;

        /// <summary>翻訳を登録する。同一種の二重登録は構成ミスとして例外。</summary>
        public RecordHubTranslator<TRecord, TKind> Map(TKind kind, TranslateHandler translate)
        {
            if (translate == null)
            {
                throw new ArgumentNullException(nameof(translate));
            }
            if (_map.ContainsKey(kind))
            {
                throw new HubException($"{kind} は登録済み（翻訳表の二重登録は構成ミス）");
            }
            _map.Add(kind, translate);
            return this;
        }

        /// <summary>「この種は翻訳しない」を明示する（黙殺との区別を表に残す）。</summary>
        public RecordHubTranslator<TRecord, TKind> MapIgnore(TKind kind)
        {
            if (_map.ContainsKey(kind))
            {
                throw new HubException($"{kind} は登録済み（翻訳表の二重登録は構成ミス）");
            }
            _map.Add(kind, null);
            return this;
        }

        /// <summary>翻訳元のログへ接続する。</summary>
        public void Attach(RecordLog<TRecord> log)
        {
            _drain.Attach(log);
        }

        /// <summary>接続を外す。</summary>
        public void Detach()
        {
            _drain.Detach();
        }

        /// <summary>新着レコードをまとめて翻訳する（毎フレームの Drain フェーズで呼ぶ）。翻訳件数を返す。</summary>
        public int Drain()
        {
            return _drain.Drain();
        }

        /// <summary>レコード1件を表引きして翻訳する。</summary>
        private void Dispatch(in TRecord record)
        {
            var kind = _kindOf(record);
            if (_map.TryGetValue(kind, out var translate))
            {
                translate?.Invoke(in record); // null は「明示的に無視」
                return;
            }
            Unmapped?.Invoke(kind);
        }
    }
}
