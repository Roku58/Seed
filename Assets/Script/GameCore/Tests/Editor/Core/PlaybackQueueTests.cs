// ============================================================================
// RecordPlaybackQueue（順次再生キュー）と並行再生部品の EditMode テスト。
// いずれも純C#なので、MonoBehaviour なしで再生の時間進行・スキップ・巻き戻しを検証できる。
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using Seed.Core;
using Seed.Core.Presenter;

namespace Seed.Core.Tests
{
    /// <summary>順次再生キューのテスト。</summary>
    public sealed class PlaybackQueueTests
    {
        /// <summary>ステップが1件ずつ、時間経過に従って順番に再生される。</summary>
        [Test]
        public void StepsPlaySequentially_ByDuration()
        {
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) =>
                {
                    var id = record;
                    return new TimedStep(1f, onStart: () => started.Add(id));
                });
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);

            queue.Tick(0.5f);
            CollectionAssert.AreEqual(new[] { 1 }, started, "1件目だけ開始している");

            queue.Tick(0.6f); // 1件目完了(累計1.1s) → 2件目が同フレームで開始
            CollectionAssert.AreEqual(new[] { 1, 2 }, started);

            queue.Tick(1.0f); // 2件目完了 → 3件目開始
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, started);
            Assert.IsFalse(queue.IsIdle, "3件目はまだ再生中");

            queue.Tick(1.0f);
            Assert.IsTrue(queue.IsIdle);
        }

        /// <summary>factory が null を返したレコードは演出なしとして即時消化される。</summary>
        [Test]
        public void NullStep_IsConsumedInstantly()
        {
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => record % 2 == 0
                    ? null // 偶数は演出なし
                    : new TimedStep(0f, onStart: () => started.Add(record)));
            for (var i = 1; i <= 5; i++) queue.Enqueue(i);

            queue.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, started, "奇数だけ再生され、全体は1Tickで完了");
            Assert.IsTrue(queue.IsIdle);
        }

        /// <summary>倍速は経過時間の掛け算として効く（結果には影響しない＝演出だけの概念）。</summary>
        [Test]
        public void SpeedMultiplier_AcceleratesPlayback()
        {
            var completed = 0;
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(1f, onComplete: () => completed++));
            queue.Enqueue(1);
            queue.SpeedMultiplier = 2f;

            queue.Tick(0.6f); // 実質1.2秒ぶん
            Assert.AreEqual(1, completed);
        }

        /// <summary>
        /// 倍速では完了時の残余時間が次ステップへ持ち越され、1フレームで複数ステップ消化される
        /// （1フレーム1ステップだと100件のログが60fpsで最低1.7秒かかる、という制約の解消）。
        /// </summary>
        [Test]
        public void SpeedMultiplier_ConsumesMultipleStepsInOneTick()
        {
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(1f, onStart: () => started.Add(record)));
            for (var i = 1; i <= 5; i++) queue.Enqueue(i);
            queue.SpeedMultiplier = 10f;

            queue.Tick(0.3f); // 実質3.0秒ぶん → 1秒ステップを3件消化し、4件目が開始される
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, started, "3件完了＋4件目開始");
            Assert.AreEqual(1, queue.PendingCount, "5件目はまだ待機");
            Assert.IsFalse(queue.IsIdle);
        }

        /// <summary>MaxStepsPerTick は1フレームの消化数を打ち止めにする（暴走防止の安全弁）。</summary>
        [Test]
        public void MaxStepsPerTick_CapsStepsInOneTick()
        {
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(0.01f, onStart: () => started.Add(record)));
            for (var i = 1; i <= 20; i++) queue.Enqueue(i);
            queue.MaxStepsPerTick = 3;

            queue.Tick(1f); // 時間は十分あるが3ステップで打ち止め
            Assert.AreEqual(3, started.Count, "上限どおり3件だけ消化される");
            Assert.AreEqual(17, queue.PendingCount);

            queue.MaxStepsPerTick = 0;
            Assert.AreEqual(1, queue.MaxStepsPerTick, "0以下は1に丸める（進まなくなるのを防ぐ）");
        }

        /// <summary>
        /// SkipAll は「早回し」。再生中ステップも未再生レコードも終端処理まで走らせてアイドルになる
        /// （ガード表示のように開始で入り終了で戻す演出が、入ったまま残らないようにするため）。
        /// </summary>
        [Test]
        public void SkipAll_CompletesRemainingSteps()
        {
            var started = new List<int>();
            var completed = new List<int>();
            var drained = 0;
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(
                    10f,
                    onStart: () => started.Add(record),
                    onComplete: () => completed.Add(record)));
            queue.Drained += () => drained++;
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);

            queue.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { 1 }, started, "1件目が再生中");
            Assert.IsFalse(queue.IsIdle);

            queue.SkipAll();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, started, "未再生ステップも開始処理が走る");
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, completed, "全ステップの終端処理が走る");
            Assert.IsTrue(queue.IsIdle);
            Assert.AreEqual(1, drained, "アイドルへ落ちたので入力ロック解除フックが鳴る");
        }

        /// <summary>DiscardAll は終端処理なしで捨てる（画面を作り直す場面向けの逃げ道）。</summary>
        [Test]
        public void DiscardAll_DropsStepsWithoutCompleting()
        {
            var completed = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(10f, onComplete: () => completed.Add(record)));
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Tick(0.1f);

            queue.DiscardAll();
            CollectionAssert.IsEmpty(completed, "終端処理は走らない");
            Assert.IsTrue(queue.IsIdle);
        }

        /// <summary>DiscardFrom は指定ログ位置以降の待機レコードだけを捨てる（手動投入は残す）。</summary>
        [Test]
        public void DiscardFrom_RemovesPendingRecordsFromLogIndex()
        {
            var log = new RecordLog<int>();
            log.Add(10);
            log.Add(20);
            log.Add(30);
            log.Add(40);

            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(0f, onStart: () => started.Add(record)));
            var cursor = 0;
            Assert.AreEqual(4, queue.EnqueueFrom(log, ref cursor));
            queue.Enqueue(999); // ログ由来でない手動投入

            Assert.AreEqual(2, queue.DiscardFrom(2), "ログ位置2以降の2件が消える");
            Assert.AreEqual(3, queue.PendingCount, "残り2件＋手動投入1件");

            queue.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { 10, 20, 999 }, started, "手動投入は巻き戻しの対象外");
            Assert.IsTrue(queue.IsIdle);
        }

        /// <summary>AttachRollbackSync 中は TruncateTo に自動追従し、切り詰め済みレコードの演出は流れない。</summary>
        [Test]
        public void RollbackSync_DiscardsPendingOnTruncate()
        {
            var log = new RecordLog<int>();
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(0f, onStart: () => started.Add(record)));
            queue.AttachRollbackSync(log);

            log.Add(10);
            log.Add(20);
            log.Add(30);
            var cursor = 0;
            queue.EnqueueFrom(log, ref cursor);

            log.TruncateTo(1); // 巻き戻し → 待機中の 20/30 が自動で消える
            Assert.AreEqual(1, queue.PendingCount, "切り詰め済みレコードは待機から消える");

            queue.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { 10 }, started, "存在しなくなったレコードの演出は流れない");

            queue.DetachRollbackSync();
            cursor = 0;
            queue.EnqueueFrom(log, ref cursor);
            log.TruncateTo(0);
            Assert.AreEqual(1, queue.PendingCount, "購読解除後は自動破棄されない");
        }

        /// <summary>IsPaused の間は時間が進まない（ポーズメニュー用）。</summary>
        [Test]
        public void IsPaused_StopsProgress()
        {
            var started = new List<int>();
            var queue = new RecordPlaybackQueue<int>(
                (int record) => new TimedStep(1f, onStart: () => started.Add(record)));
            queue.Enqueue(1);

            queue.IsPaused = true;
            queue.Tick(2f);
            CollectionAssert.IsEmpty(started, "一時停止中は開始すらしない");

            queue.IsPaused = false;
            queue.Tick(2f);
            CollectionAssert.AreEqual(new[] { 1 }, started);
            Assert.IsTrue(queue.IsIdle);
        }

        /// <summary>Drained は再生完了の瞬間に1回だけ鳴る（入力ロック解除のフック）。</summary>
        [Test]
        public void Drained_FiresOnceWhenPlaybackFinishes()
        {
            var drained = 0;
            var queue = new RecordPlaybackQueue<int>((int record) => new TimedStep(1f));
            queue.Drained += () => drained++;
            queue.Enqueue(1);

            queue.Tick(0.5f);
            Assert.AreEqual(0, drained, "再生中は鳴らない");

            queue.Tick(0.6f);
            Assert.AreEqual(1, drained, "空になった瞬間に1回");

            queue.Tick(1f);
            Assert.AreEqual(1, drained, "アイドルが続くだけでは再発火しない");
        }

        /// <summary>EnqueueFrom はカーソル以降の新着だけを取り込む。</summary>
        [Test]
        public void EnqueueFrom_TakesOnlyNewRecords()
        {
            var log = new RecordLog<int>();
            log.Add(1);
            log.Add(2);

            var queue = new RecordPlaybackQueue<int>((int record) => null);
            var cursor = 0;
            Assert.AreEqual(2, queue.EnqueueFrom(log, ref cursor));

            log.Add(3);
            Assert.AreEqual(1, queue.EnqueueFrom(log, ref cursor), "新着の1件だけ");
            Assert.AreEqual(0, queue.EnqueueFrom(log, ref cursor), "追いついたら0件");
        }

        /// <summary>CompositeStep は子を並行に走らせ、全部終わるまで完了しない。</summary>
        [Test]
        public void CompositeStep_CompletesWhenAllChildrenFinish()
        {
            var fastDone = 0;
            var slowDone = 0;
            var composite = new CompositeStep()
                .Add(new TimedStep(0.5f, onComplete: () => fastDone++))
                .Add(new TimedStep(1.5f, onComplete: () => slowDone++));

            Assert.IsFalse(composite.Tick(1f), "遅い子が残っているので未完了");
            Assert.AreEqual(1, fastDone, "速い子は先に終端まで進む");
            Assert.AreEqual(0, slowDone);

            Assert.IsTrue(composite.Tick(1f), "全ての子が終わって初めて完了");
            Assert.AreEqual(1, slowDone);
        }

        /// <summary>CompositeStep.Complete は全ての子を終端状態まで進める（未開始の子も含む）。</summary>
        [Test]
        public void CompositeStep_Complete_FinishesAllChildren()
        {
            var trace = new List<string>();
            var composite = new CompositeStep()
                .Add(new TimedStep(10f, onStart: () => trace.Add("a:start"), onComplete: () => trace.Add("a:end")))
                .Add(new TimedStep(10f, onStart: () => trace.Add("b:start"), onComplete: () => trace.Add("b:end")));

            composite.Complete();
            CollectionAssert.AreEqual(
                new[] { "a:start", "a:end", "b:start", "b:end" }, trace,
                "未開始の子も開始→終端の順で辻褄を合わせる");

            Assert.IsTrue(composite.Tick(1f), "完了後の Tick は完了を返すだけ");
            Assert.AreEqual(4, trace.Count, "終端処理が二重に走らない");
        }

        /// <summary>PlaybackChannels は全チャンネルがアイドルのときだけ IsIdle になる。</summary>
        [Test]
        public void PlaybackChannels_IsIdleOnlyWhenAllChannelsIdle()
        {
            var ui = new RecordPlaybackQueue<int>((int record) => new TimedStep(0.5f));
            var camera = new RecordPlaybackQueue<int>((int record) => new TimedStep(1.5f));
            var channels = new PlaybackChannels<int>();
            channels.Add(0, ui);
            channels.Add(1, camera);
            Assert.IsTrue(channels.IsIdle, "何も積んでいなければアイドル");
            Assert.AreSame(ui, channels.Get(0), "IDでキューを引ける");

            ui.Enqueue(1);
            camera.Enqueue(2);
            channels.Tick(1f);
            Assert.IsTrue(ui.IsIdle, "短い演出のチャンネルは先に終わる");
            Assert.IsFalse(channels.IsIdle, "1つでも動いていれば全体はアイドルでない");

            channels.Tick(1f);
            Assert.IsTrue(channels.IsIdle, "全チャンネルが終わって初めてアイドル");
        }
    }
}
