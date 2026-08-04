using System.Collections.Generic;
using NUnit.Framework;
using Seed.Character;
using UnityEngine;

namespace Seed.Motion.Tests
{
    /// <summary>Seed.Motion（IKソルバー・クロスフェード・‰イベント・対応表）のテスト。純数学。</summary>
    public sealed class MotionTests
    {
        // ================================================================
        // TwoBoneIK（解析解）
        // ================================================================

        /// <summary>届く目標: 先端が目標へ一致し、ボーン長が保存される。</summary>
        [Test]
        public void TwoBone_ReachableTarget_TipHitsTarget()
        {
            var a = Vector3.zero;
            var b = new Vector3(0f, 1f, 0f);
            var c = new Vector3(0f, 2f, 0f);
            var target = new Vector3(1f, 1f, 0f); // 届く（全長2 > 距離√2）
            TwoBoneIkSolver.Solve(a, b, c, target, poleHint: new Vector3(0f, 1f, -1f),
                out var newB, out var newC);

            Assert.AreEqual(0f, Vector3.Distance(newC, target), 0.001f, "先端が目標に着く");
            Assert.AreEqual(1f, Vector3.Distance(a, newB), 0.001f, "上腕長が保存される");
            Assert.AreEqual(1f, Vector3.Distance(newB, newC), 0.001f, "前腕長が保存される");
        }

        /// <summary>届かない目標: 一直線に伸びて最も近い点でクランプされる。</summary>
        [Test]
        public void TwoBone_UnreachableTarget_ClampsStraight()
        {
            var a = Vector3.zero;
            var b = new Vector3(0f, 1f, 0f);
            var c = new Vector3(0f, 2f, 0f);
            var target = new Vector3(10f, 0f, 0f); // 全長2では届かない
            TwoBoneIkSolver.Solve(a, b, c, target, new Vector3(0f, 0f, -1f),
                out var newB, out var newC);

            Assert.AreEqual(2f, Vector3.Distance(a, newC), 0.001f, "全長いっぱいに伸びる");
            var direction = (target - a).normalized;
            Assert.AreEqual(0f, Vector3.Cross(direction, (newB - a).normalized).magnitude, 0.001f,
                "一直線（肘が曲がらない）");
        }

        /// <summary>ポールヒント: 肘がヒント側へ曲がる。</summary>
        [Test]
        public void TwoBone_PoleHint_ControlsBendSide()
        {
            var a = Vector3.zero;
            var b = new Vector3(0f, 1f, 0f);
            var c = new Vector3(0f, 2f, 0f);
            var target = new Vector3(1.2f, 1.2f, 0f);

            TwoBoneIkSolver.Solve(a, b, c, target, poleHint: new Vector3(0f, 0f, -5f),
                out var bendBack, out _);
            TwoBoneIkSolver.Solve(a, b, c, target, poleHint: new Vector3(0f, 0f, 5f),
                out var bendFront, out _);

            Assert.Less(bendBack.z, 0f, "背面ヒントなら肘は背面へ");
            Assert.Greater(bendFront.z, 0f, "前面ヒントなら肘は前面へ");
        }

        // ================================================================
        // FABRIK（チェーンIK）
        // ================================================================

        /// <summary>届く目標へ収束し、全ボーン長が保存される。</summary>
        [Test]
        public void Fabrik_Converges_AndPreservesLengths()
        {
            var positions = new[]
            {
                Vector3.zero,
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 2f, 0f),
                new Vector3(0f, 3f, 0f),
            };
            var target = new Vector3(1.5f, 1.5f, 0.5f);
            FabrikSolver.Solve(positions, target, iterations: 16);

            Assert.AreEqual(0f, Vector3.Distance(positions[3], target), 0.01f, "先端が目標へ収束");
            Assert.AreEqual(Vector3.zero, positions[0], "根本は固定");
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, Vector3.Distance(positions[i], positions[i + 1]), 0.001f,
                    $"ボーン{i}の長さが保存される");
            }
        }

        /// <summary>届かない目標: 一直線に伸びる。</summary>
        [Test]
        public void Fabrik_Unreachable_StretchesStraight()
        {
            var positions = new[]
            {
                Vector3.zero,
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 2f, 0f),
            };
            FabrikSolver.Solve(positions, new Vector3(10f, 0f, 0f));

            Assert.AreEqual(new Vector3(1f, 0f, 0f), positions[1], "1関節目が目標方向へ");
            Assert.AreEqual(new Vector3(2f, 0f, 0f), positions[2], "先端は全長でクランプ");
        }

        // ================================================================
        // LookAt（注視）
        // ================================================================

        /// <summary>重み1・可動域内なら目標へ完全に向く。重み0なら中立のまま。</summary>
        [Test]
        public void LookAt_FullWeight_FacesTarget()
        {
            var neutral = Quaternion.identity; // forward=+Z
            var solved = LookAtSolver.Solve(neutral, Vector3.zero,
                new Vector3(1f, 0f, 1f), maxAngleDegrees: 90f, weight: 1f);
            var angle = Vector3.Angle(solved * Vector3.forward,
                new Vector3(1f, 0f, 1f).normalized);
            Assert.Less(angle, 0.1f, "目標方向を向く");

            var kept = LookAtSolver.Solve(neutral, Vector3.zero,
                new Vector3(1f, 0f, 1f), 90f, weight: 0f);
            Assert.AreEqual(0f, Quaternion.Angle(neutral, kept), 0.01f, "重み0は中立のまま");
        }

        /// <summary>可動域: 最大角を超える目標にはクランプされる（首がもげない）。</summary>
        [Test]
        public void LookAt_ClampsToMaxAngle()
        {
            var neutral = Quaternion.identity;
            var solved = LookAtSolver.Solve(neutral, Vector3.zero,
                new Vector3(0f, 0f, -1f), maxAngleDegrees: 60f, weight: 1f); // 真後ろ=180度
            Assert.AreEqual(60f, Quaternion.Angle(neutral, solved), 0.5f, "60度でクランプ");
        }

        // ================================================================
        // クロスフェード＋正規化‰イベント
        // ================================================================

        /// <summary>フェード: 重みが 0→1 へ線形に進み、完了後は1で張り付く。</summary>
        [Test]
        public void Crossfade_WeightRampsLinearly()
        {
            var state = new CrossfadeState();
            state.Play(new MotionDescriptor(1f, loop: false), fadeSeconds: 0.2f);
            Assert.AreEqual(0f, state.CurrentWeight, 0.001f);

            state.Tick(0.1f);
            Assert.AreEqual(0.5f, state.CurrentWeight, 0.001f, "半分で0.5");
            state.Tick(0.1f);
            Assert.AreEqual(1f, state.CurrentWeight, 0.001f, "完了で1");
            state.Tick(0.5f);
            Assert.AreEqual(1f, state.CurrentWeight, 0.001f, "以後は1のまま");
        }

        /// <summary>‰イベント: 通過した瞬間に1回だけ発火する（当たり判定窓の土台）。</summary>
        [Test]
        public void MotionEvents_FireOncePerCrossing()
        {
            var fired = new List<int>();
            var state = new CrossfadeState();
            state.Play(new MotionDescriptor(1f, loop: false, new[]
            {
                new MotionEvent(300, AvatarEventId.HitboxBegin),
                new MotionEvent(600, AvatarEventId.HitboxEnd),
            }), 0f);

            state.Tick(0.2f, fired.Add); // 0→200‰
            Assert.IsEmpty(fired);
            state.Tick(0.2f, fired.Add); // →400‰: HitboxBegin
            CollectionAssert.AreEqual(new[] { AvatarEventId.HitboxBegin }, fired);
            state.Tick(0.3f, fired.Add); // →700‰: HitboxEnd
            CollectionAssert.AreEqual(
                new[] { AvatarEventId.HitboxBegin, AvatarEventId.HitboxEnd }, fired);
            state.Tick(0.5f, fired.Add); // 端まで: 追加発火なし
            Assert.AreEqual(2, fired.Count);
        }

        /// <summary>ループ折り返し: 末尾側→先頭側の順で漏らさず発火する。</summary>
        [Test]
        public void MotionEvents_LoopWrap_FiresTailThenHead()
        {
            var fired = new List<int>();
            var state = new CrossfadeState();
            state.Play(new MotionDescriptor(1f, loop: true, new[]
            {
                new MotionEvent(100, AvatarEventId.Footstep),
                new MotionEvent(900, AvatarEventId.ComboWindowBegin),
            }), 0f);

            state.Tick(0.5f, fired.Add);  // →500‰: 100 のみ
            state.Tick(0.7f, fired.Add);  // →1200‰=折り返し200‰: 900（末尾）→100（先頭）
            CollectionAssert.AreEqual(new[]
            {
                AvatarEventId.Footstep,
                AvatarEventId.ComboWindowBegin,
                AvatarEventId.Footstep,
            }, fired, "折り返しは末尾→先頭の順で取りこぼさない");
        }

        /// <summary>決定性: 同じ刻みなら発火列が完全一致する。</summary>
        [Test]
        public void MotionEvents_AreDeterministic()
        {
            List<int> Run()
            {
                var fired = new List<int>();
                var state = new CrossfadeState();
                state.Play(new MotionDescriptor(0.8f, loop: true, new[]
                {
                    new MotionEvent(250, 1), new MotionEvent(750, 2),
                }), 0.1f);
                for (var i = 0; i < 40; i++)
                {
                    state.Tick(0.033f, fired.Add);
                }
                return fired;
            }
            CollectionAssert.AreEqual(Run(), Run());
        }

        // ================================================================
        // 対応表
        // ================================================================

        /// <summary>MotionBindings: 明示が無ければ同値素通し、Bind すれば上書き。</summary>
        [Test]
        public void MotionBindings_PassThrough_AndOverride()
        {
            Assert.AreEqual(BehaviorKey.Attack.Value,
                MotionBindings.Default.Resolve(BehaviorKey.Attack).Value, "既定は同値素通し");
            Assert.AreEqual(120, MotionBindings.Default.Resolve(new BehaviorKey(120)).Value,
                "アプリ独自キーも素通し（基盤変更ゼロで増える）");

            var bindings = new MotionBindings().Bind(BehaviorKey.Attack, new MotionClipId(105));
            Assert.AreEqual(105, bindings.Resolve(BehaviorKey.Attack).Value, "明示は上書き");
        }

        // ================================================================
        // 揺れもの（SpringBoneChain）
        // ================================================================

        /// <summary>テスト用の水平チェーン（0.5m×3節）。</summary>
        private static Vector3[] HorizontalChain()
        {
            return new[]
            {
                Vector3.zero,
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1.0f, 0f, 0f),
                new Vector3(1.5f, 0f, 0f),
            };
        }

        /// <summary>無重力・静止入力なら、アニメ姿勢に留まる（勝手に揺れない）。</summary>
        [Test]
        public void Spring_RestPose_StaysAtRest()
        {
            var animated = HorizontalChain();
            var parameters = SpringBoneParams.Default;
            parameters.Gravity = Vector3.zero;
            var chain = new SpringBoneChain(animated, parameters);
            for (var i = 0; i < 120; i++)
            {
                chain.Step(1f / 60f, animated);
            }
            for (var i = 0; i < animated.Length; i++)
            {
                Assert.AreEqual(0f, Vector3.Distance(animated[i], chain.GetPosition(i)), 1e-3f,
                    $"関節{i}は静止したまま");
            }
        }

        /// <summary>復元: アニメ姿勢が変わると、揺れながら新しい姿勢へ収束する。</summary>
        [Test]
        public void Spring_FollowsNewAnimatedPose()
        {
            var down = new[]
            {
                Vector3.zero,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(0f, -1.0f, 0f),
                new Vector3(0f, -1.5f, 0f),
            };
            var parameters = SpringBoneParams.Default;
            parameters.Gravity = Vector3.zero;
            var chain = new SpringBoneChain(down, parameters);
            var target = HorizontalChain();
            for (var i = 0; i < 600; i++)
            {
                chain.Step(1f / 60f, target);
            }
            Assert.AreEqual(0f, Vector3.Distance(target[3], chain.GetPosition(3)), 0.05f,
                "先端が新しいアニメ姿勢へ収束する");
        }

        /// <summary>重力: 水平のアニメ姿勢でも先端が垂れ下がる（マント・尻尾の質感）。</summary>
        [Test]
        public void Spring_Gravity_DroopsTip()
        {
            var animated = HorizontalChain();
            var parameters = SpringBoneParams.Default;
            parameters.Stiffness = 4f;
            parameters.Gravity = new Vector3(0f, -9.8f, 0f);
            var chain = new SpringBoneChain(animated, parameters);
            for (var i = 0; i < 600; i++)
            {
                chain.Step(1f / 60f, animated);
            }
            Assert.Less(chain.GetPosition(3).y, -0.3f, "先端が垂れる");
            Assert.AreEqual(0f, chain.GetPosition(0).y, 1e-4f, "根本はアニメに固定");
        }

        /// <summary>長さ拘束: どれだけ揺さぶってもボーン長が伸び縮みしない。</summary>
        [Test]
        public void Spring_PreservesBoneLengths()
        {
            var animated = HorizontalChain();
            var chain = new SpringBoneChain(animated, SpringBoneParams.Default);
            var swung = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f),
                new Vector3(1.0f, 0f, 1f),
                new Vector3(1.5f, 0f, 1f),
            };
            for (var i = 0; i < 90; i++)
            {
                chain.Step(1f / 60f, i % 2 == 0 ? animated : swung); // 激しく揺さぶる
            }
            for (var i = 1; i < animated.Length; i++)
            {
                Assert.AreEqual(0.5f,
                    Vector3.Distance(chain.GetPosition(i - 1), chain.GetPosition(i)), 1e-3f,
                    $"節{i}の長さが保存される");
            }
        }

        /// <summary>球コライダー: 先端がめり込まず（半径＋関節半径の外へ）押し出される。</summary>
        [Test]
        public void Spring_Collider_PushesOut()
        {
            var animated = HorizontalChain();
            var parameters = SpringBoneParams.Default;
            parameters.Gravity = Vector3.zero;
            parameters.JointRadius = 0.05f;
            var chain = new SpringBoneChain(animated, parameters);
            var spheres = new[] { new SpringSphere(new Vector3(1.5f, 0f, 0f), 0.3f) };
            for (var i = 0; i < 120; i++)
            {
                chain.Step(1f / 60f, animated, spheres);
            }
            Assert.GreaterOrEqual(
                Vector3.Distance(chain.GetPosition(3), spheres[0].Center), 0.35f - 0.01f,
                "先端は 球半径+関節半径 の外へ押し出される");
        }

        /// <summary>決定性: 同じ刻み・同じ入力列なら軌跡が完全一致する。</summary>
        [Test]
        public void Spring_IsDeterministic()
        {
            Vector3 Run()
            {
                var animated = HorizontalChain();
                var swung = new[]
                {
                    new Vector3(0f, 0f, 1f),
                    new Vector3(0.5f, 0f, 1f),
                    new Vector3(1.0f, 0f, 1f),
                    new Vector3(1.5f, 0f, 1f),
                };
                var chain = new SpringBoneChain(animated, SpringBoneParams.Default);
                for (var i = 0; i < 90; i++)
                {
                    chain.Step(0.017f, i % 3 == 0 ? swung : animated);
                }
                return chain.GetPosition(3);
            }
            Assert.AreEqual(Run(), Run(), "float演算まで完全一致");
        }

        /// <summary>ヒッチ耐性: 巨大なdtでも爆発しない（クランプ＋固定サブステップ）。</summary>
        [Test]
        public void Spring_HugeDeltaTime_StaysStable()
        {
            var animated = HorizontalChain();
            var chain = new SpringBoneChain(animated, SpringBoneParams.Default);
            chain.Step(5f, animated); // 5秒ヒッチ（内部で0.1秒へクランプ）
            for (var i = 1; i < animated.Length; i++)
            {
                var position = chain.GetPosition(i);
                Assert.IsFalse(float.IsNaN(position.x) || float.IsInfinity(position.magnitude),
                    "発散しない");
                Assert.AreEqual(0.5f,
                    Vector3.Distance(chain.GetPosition(i - 1), position), 1e-3f, "長さ保存");
            }
            Assert.Less(Vector3.Distance(animated[3], chain.GetPosition(3)), 1f, "吹き飛ばない");
        }

        /// <summary>FPS非依存: dtの刻み方を変えても（固定刻みへの正規化により）軌跡が一致する。</summary>
        [Test]
        public void Spring_VariableDeltaTime_MatchesFixedSteps()
        {
            var animated = HorizontalChain();
            var swung = new[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f),
                new Vector3(1.0f, 0f, 1f),
                new Vector3(1.5f, 0f, 1f),
            };
            var step = 1f / 60f; // 固定刻みの正確な倍数を使う（端数の繰り越しは決定性テストが担う）
            var coarse = new SpringBoneChain(animated, SpringBoneParams.Default);
            coarse.Step(step * 4f, swung); // 15fps相当のまとめ実行
            var fine = new SpringBoneChain(animated, SpringBoneParams.Default);
            for (var i = 0; i < 4; i++)
            {
                fine.Step(step, swung); // 60fps相当の細かい実行
            }
            for (var i = 0; i < animated.Length; i++)
            {
                Assert.AreEqual(0f,
                    Vector3.Distance(coarse.GetPosition(i), fine.GetPosition(i)), 1e-6f,
                    $"関節{i}の軌跡が刻み方に依存しない");
            }
        }

        /// <summary>Reset: ワープ後の追従リセットで即座にアニメ姿勢と一致する。</summary>
        [Test]
        public void Spring_Reset_SnapsToAnimatedPose()
        {
            var animated = HorizontalChain();
            var chain = new SpringBoneChain(animated, SpringBoneParams.Default);
            var warped = new[]
            {
                new Vector3(100f, 0f, 0f),
                new Vector3(100.5f, 0f, 0f),
                new Vector3(101f, 0f, 0f),
                new Vector3(101.5f, 0f, 0f),
            };
            chain.Step(1f / 60f, warped);
            Assert.Greater(Vector3.Distance(warped[3], chain.GetPosition(3)), 1f,
                "リセットなしでは大きく遅れる（鞭化の原因）");
            chain.Reset(warped);
            Assert.AreEqual(0f, Vector3.Distance(warped[3], chain.GetPosition(3)), 1e-4f,
                "リセットで即座に一致");
        }
    }
}
