using System.Collections.Generic;
using NUnit.Framework;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Hub.Tests
{
    /// <summary>Request-Response 標準（RequestId 相関）の規約どおりの使い方を検証する。</summary>
    public sealed class RequestResponseTests
    {
        /// <summary>依頼命令の例（XxxRequested : ICommandMessage + RequestId）。</summary>
        private readonly struct ProbeRequested : ICommandMessage
        {
            /// <summary>相関ID。</summary>
            public readonly RequestId Request;

            /// <summary>依頼の中身。</summary>
            public readonly int Value;

            /// <summary>ProbeRequested を生成する。</summary>
            public ProbeRequested(RequestId request, int value)
            {
                Request = request;
                Value = value;
            }
        }

        /// <summary>完了通知の例（XxxCompleted : INotificationMessage + 同じ RequestId）。</summary>
        private readonly struct ProbeCompleted : INotificationMessage
        {
            /// <summary>相関ID（依頼と同じ値）。</summary>
            public readonly RequestId Request;

            /// <summary>成否。</summary>
            public readonly bool Success;

            /// <summary>ProbeCompleted を生成する。</summary>
            public ProbeCompleted(RequestId request, bool success)
            {
                Request = request;
                Success = success;
            }
        }

        /// <summary>発番器は単調増加で重複しない。</summary>
        [Test]
        public void RequestIdSource_IssuesUniqueAscendingIds()
        {
            var source = new RequestIdSource();
            var first = source.Next();
            var second = source.Next();
            Assert.AreNotEqual(first.Value, second.Value);
            Assert.Greater(second.Value, first.Value);
            Assert.AreNotEqual(RequestId.None, first, "None(0) は発番されない");
        }

        /// <summary>複数の依頼が並んでも、相関IDで自分の応答だけを拾える。</summary>
        [Test]
        public void CorrelationPattern_MatchesResponsesToRequests()
        {
            var hub = new MessageHub();
            var source = new RequestIdSource();

            // 処理側（1基盤）: 依頼を受けて完了通知を返す。偶数値の依頼だけ成功とする
            hub.SubscribeCommand<ProbeRequested>(request =>
                hub.Publish(new ProbeCompleted(request.Request, request.Value % 2 == 0)));

            // 依頼側A: 自分のIDの応答だけを拾う
            var idA = source.Next();
            var resultsA = new List<bool>();
            hub.Subscribe<ProbeCompleted>(completed =>
            {
                if (completed.Request.Equals(idA))
                {
                    resultsA.Add(completed.Success);
                }
            });

            // 依頼側B（別の依頼が混ざっても A には届かない）
            var idB = source.Next();

            hub.PublishCommand(new ProbeRequested(idB, 1)); // Bの依頼（失敗になる）
            hub.PublishCommand(new ProbeRequested(idA, 2)); // Aの依頼（成功になる）

            Assert.AreEqual(1, resultsA.Count, "自分の応答だけが1件届く");
            Assert.IsTrue(resultsA[0]);
        }
    }
}
