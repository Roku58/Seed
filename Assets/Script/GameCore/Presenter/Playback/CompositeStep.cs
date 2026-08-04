using System.Collections.Generic;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// 複数ステップを「並行」に走らせ、全部完了したら完了になるステップ。
    ///
    /// [なぜ必要か] 単一ステップの直列再生だけでは「全体攻撃で3体が同時に被弾」
    /// 「カメラ寄せとUIカウントとキャラ演出が同時」を表現できず、
    /// アクション系は即時Dispatchに逃げるしかなかった。1レコードから複数の
    /// 並行演出を組み立てる口をここに集約する。
    ///
    /// - 純C#・低GC（子は List に持ち、for で回す。完了した子は null 埋めして以降触らない）
    /// - 組み立ては再生開始前に済ませる想定（Add は生成直後にチェーンで呼ぶ）
    /// <code>
    ///   return new CompositeStep()
    ///       .Add(new TimedStep(0.3f, onStart: () => viewA.PlayHit()))
    ///       .Add(new TimedStep(0.5f, onStart: () => camera.Shake()));
    /// </code>
    /// </summary>
    public sealed class CompositeStep : IPlaybackStep
    {
        /// <summary>子ステップ（完了した要素は null になる）。</summary>
        private readonly List<IPlaybackStep> _children;
        /// <summary>完了処理を実行済みか（Tick と Complete の二重処理を防ぐ）。</summary>
        private bool _finished;
        /// <summary>完了時に使い切らなかった時間（秒）。</summary>
        private float _remainder;

        /// <summary>CompositeStep を生成する。capacity は子の想定数（Listの再確保を避けるため）。</summary>
        public CompositeStep(int capacity = 4)
        {
            _children = new List<IPlaybackStep>(capacity);
        }

        /// <summary>抱えている子の数（完了済みも含む）。</summary>
        public int Count => _children.Count;

        /// <summary>完了時に使い切らなかった時間（秒）。最後に終わった子の余り＝全体の余り。</summary>
        public float Remainder => _remainder;

        /// <summary>子ステップを追加する。null は「演出なし」として黙って無視する（factoryの戻りをそのまま渡せる）。</summary>
        public CompositeStep Add(IPlaybackStep step)
        {
            if (step != null && !_finished)
            {
                _children.Add(step);
            }
            return this;
        }

        /// <summary>全ての子へ同じ経過時間を与える。全部完了したときだけ true を返す。</summary>
        public bool Tick(float deltaSeconds)
        {
            if (_finished)
            {
                return true;
            }

            var allDone = true;
            var tickedAny = false;
            var minRemainder = float.MaxValue;

            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null)
                {
                    continue; // 先のフレームで完了済み
                }

                tickedAny = true;
                if (child.Tick(deltaSeconds))
                {
                    // 最後に終わった子＝余りが最小の子。全体の余りはその値になる。
                    var remainder = child.Remainder;
                    if (remainder < minRemainder)
                    {
                        minRemainder = remainder;
                    }
                    _children[i] = null;
                }
                else
                {
                    allDone = false;
                }
            }

            if (!allDone)
            {
                _remainder = 0f;
                return false;
            }

            _finished = true;
            // 子が1つも動かなかった（空 or 全て既完了）なら時間を消費していないので全額返す。
            _remainder = tickedAny
                ? (minRemainder > 0f ? minRemainder : 0f)
                : (deltaSeconds > 0f ? deltaSeconds : 0f);
            return true;
        }

        /// <summary>全ての子を終端状態まで進めて完了する（スキップ用）。</summary>
        public void Complete()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _remainder = 0f;
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                if (child == null)
                {
                    continue;
                }
                child.Complete();
                _children[i] = null;
            }
        }
    }
}
