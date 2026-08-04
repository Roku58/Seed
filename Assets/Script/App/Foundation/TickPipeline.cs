using System;
using System.Collections.Generic;
using Seed.Hub;

namespace Seed.App
{
    /// <summary>1フレーム内の実行フェーズ（値の昇順 = 実行順）。</summary>
    public enum TickPhase
    {
        /// <summary>入力の読み取り・Logicへの保管。</summary>
        Input = 0,

        /// <summary>キャラクター等のシミュレーション駆動（CharactersManager.Tick）。</summary>
        Simulation = 1,

        /// <summary>決定的ロジックの時間前進（ジャーナル経由）。</summary>
        LogicTime = 2,

        /// <summary>レコード→通知の翻訳と、その同期連鎖（方針・演出・UI更新）。</summary>
        Drain = 3,
    }

    /// <summary>
    /// フェーズ順序つきのTickパイプライン（合成ルートの毎フレーム駆動の骨格）。
    ///
    /// ARCHITECTURE.md の「1フレームの実行順」はこれまでコメント上の規約でしかなく、
    /// 順序ミス（DrainをTickより先に呼ぶ等）をコンパイルもテストも検出できなかった。
    /// 本クラスに登録して Tick(dt) を1回呼ぶ形にすることで、実行順という規約が
    /// 型と実装で保証され、シーンが増えても同じ骨格を再利用できる。
    /// 同一フェーズ内は登録順（決定的）。
    /// </summary>
    public sealed class TickPipeline
    {
        /// <summary>登録1件。</summary>
        private readonly struct Entry
        {
            /// <summary>実行フェーズ。</summary>
            public readonly TickPhase Phase;

            /// <summary>本体。</summary>
            public readonly Action<float> Step;

            /// <summary>Entry を生成する。</summary>
            public Entry(TickPhase phase, Action<float> step)
            {
                Phase = phase;
                Step = step;
            }
        }

        /// <summary>フェーズ→登録列（登録順維持）。</summary>
        private readonly List<Entry> _entries = new List<Entry>(8);

        /// <summary>Tick 実行中か。</summary>
        private bool _isTicking;

        /// <summary>ステップを登録する（同一フェーズ内は登録順に実行される）。</summary>
        public void Add(TickPhase phase, Action<float> step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }
            if (_isTicking)
            {
                throw new HubException("Tick 中のステップ追加は禁止（実行順の決定性が壊れる）");
            }
            _entries.Add(new Entry(phase, step));
        }

        /// <summary>全ステップをフェーズ昇順→登録順で実行する（1フレームの駆動入口）。</summary>
        public void Tick(float deltaTime)
        {
            _isTicking = true;
            try
            {
                // フェーズ数は小さいので、安定順序を保ったままフェーズごとに走査する
                for (var phase = TickPhase.Input; phase <= TickPhase.Drain; phase++)
                {
                    for (var i = 0; i < _entries.Count; i++)
                    {
                        if (_entries[i].Phase == phase)
                        {
                            _entries[i].Step(deltaTime);
                        }
                    }
                }
            }
            finally
            {
                _isTicking = false;
            }
        }
    }
}
