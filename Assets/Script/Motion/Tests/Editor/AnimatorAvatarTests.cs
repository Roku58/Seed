using System.Collections.Generic;
using NUnit.Framework;
using Seed.Character;
using UnityEditor.Animations;
using UnityEngine;

namespace Seed.Motion.Tests
{
    /// <summary>
    /// AnimatorController 駆動 Avatar（AnimatorAvatar）のテスト。
    /// インメモリの AnimatorController（遷移を張らないステートのみ）を組み、
    /// 「行動遷移 → クロスフェード命令」の翻訳が正しいことを検証する。
    /// </summary>
    public sealed class AnimatorAvatarTests
    {
        /// <summary>テスト中に作ったオブジェクト（後始末用）。</summary>
        private readonly List<Object> _spawned = new List<Object>();

        /// <summary>作ったオブジェクトを毎テスト後に破棄する。</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
            _spawned.Clear();
        }

        /// <summary>行動遷移で対応する同名ステートへ移る（既定の対応表）。</summary>
        [Test]
        public void 行動遷移で対応ステートへ移る()
        {
            var avatar = CreateAvatar(out var animator, "Idle", "Attack");
            avatar.Configure(animator);

            avatar.OnBehaviorChanged(BehaviorKey.Idle, BehaviorKey.Attack);
            animator.Update(1f); // フェード時間を超えて進める

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"), Is.True);
        }

        /// <summary>対応表のカスタム束ねが効く（行動キー→任意ステート名）。</summary>
        [Test]
        public void 対応表で任意ステートへ束ねられる()
        {
            var avatar = CreateAvatar(out var animator, "Idle", "Special");
            avatar.Configure(animator,
                new AnimatorAvatarBindings().Bind(BehaviorKey.Attack, "Special"));

            avatar.OnBehaviorChanged(BehaviorKey.Idle, BehaviorKey.Attack);
            animator.Update(1f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Special"), Is.True);
        }

        /// <summary>未登録の行動キーでは何もしない（例外なし・現在ステート維持）。</summary>
        [Test]
        public void 未登録キーは現在ステートを保つ()
        {
            var avatar = CreateAvatar(out var animator, "Idle");
            avatar.Configure(animator,
                new AnimatorAvatarBindings().Bind(BehaviorKey.Idle, "Idle"));
            animator.Update(0.1f);

            Assert.DoesNotThrow(() =>
                avatar.OnBehaviorChanged(BehaviorKey.Idle, BehaviorKey.Attack));
            animator.Update(1f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }

        /// <summary>移動の正規化速度が Speed パラメータへ写る（ブレンドツリーの入力）。</summary>
        [Test]
        public void 正規化速度がパラメータへ写る()
        {
            var avatar = CreateAvatar(out var animator, "Idle");
            avatar.Configure(animator);

            avatar.SetLocomotionSpeed(0.7f);

            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0.7f).Within(1e-4f));
        }

        /// <summary>遷移を張らないステートだけの Controller と Avatar を組み上げる。</summary>
        private AnimatorAvatar CreateAvatar(out Animator animator, params string[] states)
        {
            var controller = new AnimatorController();
            _spawned.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            _spawned.Add(machine);
            foreach (var state in states)
            {
                machine.AddState(state);
            }

            var host = new GameObject("AnimatorAvatarHost");
            _spawned.Add(host);
            animator = host.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            return host.AddComponent<AnimatorAvatar>();
        }
    }
}
