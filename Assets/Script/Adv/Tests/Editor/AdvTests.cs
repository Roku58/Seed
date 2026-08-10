using System.Collections.Generic;
using NUnit.Framework;
using Seed.Adv;

namespace Seed.Adv.Tests
{
    /// <summary>ADV進行の状態機械（AdvPlayer / AdvScript）のテスト。</summary>
    public sealed class AdvTests
    {
        /// <summary>1歩ぶんの刻み（60fps相当）。</summary>
        private const float Step = 1f / 60f;

        /// <summary>2ページ＋選択肢2件の台本を作る（標準ケース）。</summary>
        private static AdvScript CreateScript()
        {
            return new AdvScript(
                new[] { "いらっしゃい。", "ゆっくり見ていって。" },
                new[] { new AdvChoice("買う", 302), new AdvChoice("出る", 0) });
        }

        /// <summary>文字送りは速度×時間で1文字ずつ進む。</summary>
        [Test]
        public void 文字送りが時間で進む()
        {
            var player = new AdvPlayer(CreateScript(), charsPerSecond: 10f);

            Assert.That(player.VisibleLength, Is.EqualTo(0));
            player.Tick(0.35f); // 10文字/秒 × 0.35秒 = 3.5 → 3文字
            Assert.That(player.VisibleLength, Is.EqualTo(3));
            Assert.That(player.State, Is.EqualTo(AdvState.Typing));
        }

        /// <summary>文字送り中の送り操作で一括全文表示になる。</summary>
        [Test]
        public void 文字送り中の送りで全文表示()
        {
            var player = new AdvPlayer(CreateScript(), charsPerSecond: 10f);
            player.Tick(0.1f);

            player.Advance();

            Assert.That(player.VisibleLength, Is.EqualTo(player.PageText.Length));
            Assert.That(player.State, Is.EqualTo(AdvState.PageComplete));
        }

        /// <summary>全文表示済みの送り操作で次ページへ進み、文字送りが最初から始まる。</summary>
        [Test]
        public void 全文表示後の送りで次ページへ()
        {
            var player = new AdvPlayer(CreateScript());
            var pageChanged = 0;
            player.PageChanged += () => pageChanged++;
            player.Advance(); // 全文表示

            player.Advance(); // 次ページ

            Assert.That(player.PageIndex, Is.EqualTo(1));
            Assert.That(pageChanged, Is.EqualTo(1));
            Assert.That(player.VisibleLength, Is.EqualTo(0), "新ページは文字送りをやり直す");
            Assert.That(player.State, Is.EqualTo(AdvState.Typing));
        }

        /// <summary>最終ページを読み終えると、選択肢があれば選択中になる。</summary>
        [Test]
        public void 最終ページ後は選択肢の提示になる()
        {
            var player = new AdvPlayer(CreateScript());
            player.Advance(); // 1ページ目全文
            player.Advance(); // 2ページ目へ
            player.Advance(); // 2ページ目全文

            player.Advance(); // 読了

            Assert.That(player.State, Is.EqualTo(AdvState.Choosing));
            Assert.That(player.Choices.Count, Is.EqualTo(2));
        }

        /// <summary>選択肢なしの台本（強制イベント）は読了で ResultId=0 の終了になる。</summary>
        [Test]
        public void 強制イベントは読了で終了する()
        {
            var player = new AdvPlayer(new AdvScript(new[] { "回復薬を手に入れた。" }));
            var finished = -1;
            player.Finished += id => finished = id;

            player.Advance(); // 全文表示
            player.Advance(); // 読了

            Assert.That(player.State, Is.EqualTo(AdvState.Finished));
            Assert.That(finished, Is.EqualTo(0));
        }

        /// <summary>選択の確定で、選んだ選択肢の ResultId を伴って終了する。</summary>
        [Test]
        public void 選択の確定で結果IDが届く()
        {
            var player = new AdvPlayer(CreateScript());
            var finished = -1;
            player.Finished += id => finished = id;
            player.Advance();
            player.Advance();
            player.Advance();
            player.Advance(); // Choosing へ

            player.Select(0);

            Assert.That(player.State, Is.EqualTo(AdvState.Finished));
            Assert.That(finished, Is.EqualTo(302));
            Assert.That(player.ResultId, Is.EqualTo(302));
        }

        /// <summary>選択中以外・範囲外の Select は無視される（誤操作の安全域）。</summary>
        [Test]
        public void 選択中以外のSelectは無視される()
        {
            var player = new AdvPlayer(CreateScript());

            player.Select(0); // まだ文字送り中
            Assert.That(player.State, Is.EqualTo(AdvState.Typing));

            player.Advance();
            player.Advance();
            player.Advance();
            player.Advance(); // Choosing へ
            player.Select(99); // 範囲外
            Assert.That(player.State, Is.EqualTo(AdvState.Choosing));
        }

        /// <summary>速度0以下は瞬間表示の仕様（演出を切りたいデータ向け）。</summary>
        [Test]
        public void 速度0以下は瞬間表示になる()
        {
            var player = new AdvPlayer(CreateScript(), charsPerSecond: 0f);

            player.Tick(Step);

            Assert.That(player.VisibleLength, Is.EqualTo(player.PageText.Length));
            Assert.That(player.State, Is.EqualTo(AdvState.PageComplete));
        }

        /// <summary>同じ刻み列なら表示文字数の推移が完全一致する（決定性）。</summary>
        [Test]
        public void 同じ刻み列なら進行が一致する()
        {
            var a = new AdvPlayer(CreateScript(), charsPerSecond: 23.7f);
            var b = new AdvPlayer(CreateScript(), charsPerSecond: 23.7f);

            var historyA = new List<int>();
            var historyB = new List<int>();
            for (var i = 0; i < 60; i++)
            {
                a.Tick(Step);
                b.Tick(Step);
                historyA.Add(a.VisibleLength);
                historyB.Add(b.VisibleLength);
            }

            Assert.That(historyA, Is.EqualTo(historyB));
        }

        /// <summary>会話ページは話者・見せ方・演技を台本から読める。</summary>
        [Test]
        public void 会話ページの話者と演技が読める()
        {
            var acts = new[] { new AdvAct(2, AdvActKind.Move, x: -0.8f, z: 2.3f, seconds: 1.4f) };
            var script = new AdvScript(new[]
            {
                new AdvPage("いらっしゃい。", speakerId: 1, AdvSpeechStyle.Bubble, acts),
            });
            var player = new AdvPlayer(script);

            Assert.That(player.CurrentPage.SpeakerId, Is.EqualTo(1));
            Assert.That(player.CurrentPage.Style, Is.EqualTo(AdvSpeechStyle.Bubble));
            Assert.That(player.CurrentPage.Acts.Count, Is.EqualTo(1));
            Assert.That(player.CurrentPage.Acts[0].Kind, Is.EqualTo(AdvActKind.Move));
        }

        /// <summary>演技の寿命（ページ切替・送り操作・時間）が台本から読める。</summary>
        [Test]
        public void 演技の寿命が台本から読める()
        {
            var acts = new[]
            {
                new AdvAct(1, AdvActKind.Bubble, "……", seconds: 2f, life: AdvActLife.Page),
                new AdvAct(0, AdvActKind.CameraReset, seconds: 0.5f),
            };
            var script = new AdvScript(new[] { new AdvPage("本文", 1, AdvSpeechStyle.Bubble, acts) });

            Assert.That(script.Pages[0].Acts[0].Life, Is.EqualTo(AdvActLife.Page));
            Assert.That(script.Pages[0].Acts[0].Seconds, Is.EqualTo(2f));
            Assert.That(script.Pages[0].Acts[1].Kind, Is.EqualTo(AdvActKind.CameraReset));
            Assert.That(script.Pages[0].Acts[1].Life, Is.EqualTo(AdvActLife.Keep), "既定は閉じ命令まで残す");
        }

        /// <summary>文字列だけの台本は「窓表示・話者なし・演技なし」に包まれる。</summary>
        [Test]
        public void 文字列だけの台本は窓表示になる()
        {
            var player = new AdvPlayer(new AdvScript(new[] { "こんにちは" }));

            Assert.That(player.CurrentPage.Style, Is.EqualTo(AdvSpeechStyle.Window));
            Assert.That(player.CurrentPage.SpeakerId, Is.EqualTo(0));
            Assert.That(player.CurrentPage.Acts.Count, Is.EqualTo(0));
        }

        /// <summary>ページ0件の台本はデータ不備として生成時に即例外。</summary>
        [Test]
        public void ページ0件は生成時に例外()
        {
            Assert.That(() => new AdvScript(new string[0]),
                Throws.ArgumentException);
        }
    }
}
